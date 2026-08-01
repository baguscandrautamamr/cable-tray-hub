/**
 * ====================================================================
 * CABLE TRAY PROJECT HUB - BACKEND SCRIPT (ONLINE API EDITION)
 * ====================================================================
 * Backend controller yang melayani request CRUD untuk data master kabel,
 * template tray, kalkulasi simulasi penataan kabel, serta riwayat simulasi.
 *
 * MODE OPERASI:
 * 1. Web App GAS  : buka URL /exec tanpa parameter -> tampil HTML app.
 * 2. REST API JSON: untuk web eksternal (Vercel) & plugin Revit.
 *    - GET  ?action=ping                      -> cek koneksi
 *    - GET  ?action=getInitialData            -> semua kabel/tray/riwayat
 *    - GET  ?action=getSimulation&id=SIM-xxx  -> detail 1 simulasi (untuk Revit)
 *    - POST body: {action:"saveSimulation", data:{...}} -> simpan riwayat
 *    - POST body: {action:"deleteSimulation", data:{id:"SIM-xxx"}} -> hapus riwayat
 * ====================================================================
 */

function doGet(e) {
  // ---- MODE REST API (dipakai oleh Vercel & plugin Revit) ----
  if (e && e.parameter && e.parameter.action) {
    return handleApiGet(e);
  }

  // ---- MODE WEB APP BAWAAN GAS ----
  var template = HtmlService.createTemplateFromFile('index');
  return template.evaluate()
    .setTitle('Cable Tray Project Hub Ultimate')
    .setSandboxMode(HtmlService.SandboxMode.IFRAME)
    .addMetaTag('viewport', 'width=device-width, initial-scale=1.0')
    .setXFrameOptionsMode(HtmlService.XFrameOptionsMode.ALLOWALL);
}

function doPost(e) {
  try {
    var body = JSON.parse(e.postData.contents);
    if (body.action === "saveSimulation") {
      return jsonOutput(saveSimulation(body.data));
    }
    if (body.action === "pushRevitStatus") {
      return jsonOutput(pushRevitStatus(body.data));
    }
    if (body.action === "deleteSimulation") {
      return jsonOutput(deleteSimulation(body.data));
    }
    // ---- Gerbang login & manajemen user ----
    if (body.action === "login")          return jsonOutput(apiLogin(body.data));
    if (body.action === "logout")         return jsonOutput(apiLogout(body.data));
    if (body.action === "listUsers")      return jsonOutput(apiListUsers(body.data));
    if (body.action === "addUser")        return jsonOutput(apiAddUser(body.data));
    if (body.action === "deleteUser")     return jsonOutput(apiDeleteUser(body.data));
    if (body.action === "setUserActive")  return jsonOutput(apiSetUserActive(body.data));
    if (body.action === "changePassword") return jsonOutput(apiChangePassword(body.data));
    return jsonOutput({ success: false, error: "Unknown action: " + body.action });
  } catch (error) {
    return jsonOutput({ success: false, error: error.toString() });
  }
}

/**
 * Menerima laporan PUSH dari plugin Revit: status tiap jalur yang sudah
 * digambar di model (jumlah conduit, total panjang, nama model, user).
 * Dicatat ke sheet "Revit_Sync" (dibuat otomatis bila belum ada).
 */
function pushRevitStatus(data) {
  try {
    var ss = SpreadsheetApp.getActiveSpreadsheet();
    var sheet = ss.getSheetByName("Revit_Sync");
    if (!sheet) {
      sheet = ss.insertSheet("Revit_Sync");
      var header = ["Tanggal", "ID_Simulasi", "Jalur", "Status",
                    "Jumlah_Conduit", "Total_Panjang_m", "Model_Revit", "Oleh_User"];
      sheet.appendRow(header);
      sheet.getRange(1, 1, 1, header.length)
        .setFontWeight("bold").setBackground("#2563EB").setFontColor("#FFFFFF");
    }

    var now = new Date();
    var routes = data.routes || [];
    for (var i = 0; i < routes.length; i++) {
      var r = routes[i];
      sheet.appendRow([
        now,
        data.simId || "",
        r.key || "",
        r.status || "TERGAMBAR",
        r.jumlahConduit || 0,
        r.totalPanjangM || 0,
        data.model || "",
        data.user || ""
      ]);
    }

    SpreadsheetApp.flush();
    return { success: true, saved: routes.length };
  } catch (error) {
    return { success: false, error: error.toString() };
  }
}

function handleApiGet(e) {
  var action = e.parameter.action;
  if (action === "ping") {
    return jsonOutput({ success: true, service: "CableTrayHub API", time: new Date().toISOString() });
  }
  if (action === "getInitialData") {
    return jsonOutput(getInitialData());
  }
  if (action === "getSimulation") {
    return jsonOutput(getSimulationById(e.parameter.id));
  }
  return jsonOutput({ success: false, error: "Unknown action: " + action });
}

function jsonOutput(obj) {
  return ContentService
    .createTextOutput(JSON.stringify(obj))
    .setMimeType(ContentService.MimeType.JSON);
}

/**
 * Parser numerik defensif agar sel kosong tidak merusak kalkulasi
 */
function safeParse(val) {
  if (val === null || val === undefined) return 0;
  var str = val.toString().trim();
  var num = parseFloat(str.replace(/[^\d\.\-]/g, ''));
  return isNaN(num) ? 0 : num;
}

/**
 * Mengubah satu baris sheet Simulasi_History menjadi objek riwayat
 */
function mapHistoryRow(r, tz) {
  var dateVal = r[1];
  var dateString = "";
  if (dateVal instanceof Date) {
    dateString = Utilities.formatDate(dateVal, tz, "dd/MM/yyyy HH:mm:ss");
  } else {
    dateString = dateVal ? dateVal.toString() : "";
  }

  var detail = null;
  if (r.length > 12 && r[12]) {
    try { detail = JSON.parse(r[12].toString()); } catch (err) { detail = null; }
  }

  return {
    id: r[0].toString(),
    tanggal: dateString,
    namaProyek: r[2] ? r[2].toString() : "",
    jenisTray: r[3] ? r[3].toString() : "",
    lebarTray: safeParse(r[4]),
    metode: r[5] ? r[5].toString() : "Flat Touching",
    persenSpare: r[6] ? r[6].toString() : "0%",
    totalLebarKabel: safeParse(r[7]),
    totalBerat: safeParse(r[8]),
    status: r[9] ? r[9].toString() : "AMAN",
    rekomendasi: r[10] ? r[10].toString() : "-",
    user: r[11] ? r[11].toString() : "Unknown",
    detail: detail
  };
}

/**
 * Mendapatkan semua data inisiasi awal untuk dashboard dan form simulasi
 */
function getInitialData() {
  try {
    var ss = SpreadsheetApp.getActiveSpreadsheet();
    var tz = ss.getSpreadsheetTimeZone();

    // Inisialisasi fallback array data
    var kabelList = [];
    var trayList = [];
    var historyList = [];

    // 1. Ambil data Kabel
    var sheetKabel = ss.getSheetByName("Katalog_Kabel");
    if (sheetKabel) {
      var dataKabel = sheetKabel.getDataRange().getValues();
      for (var i = 1; i < dataKabel.length; i++) {
        var r = dataKabel[i];
        if (r[0]) {
          kabelList.push({
            id: r[0].toString(),
            nama: r[1] ? r[1].toString() : "",
            tipeCore: r[2] ? r[2].toString() : "Single Core",
            diameter: safeParse(r[3]),
            maxCurrent: safeParse(r[4]),
            berat: safeParse(r[5])
          });
        }
      }
    }

    // 2. Ambil data Tray
    var sheetTray = ss.getSheetByName("Tray_Templates");
    if (sheetTray) {
      var dataTray = sheetTray.getDataRange().getValues();
      for (var i = 1; i < dataTray.length; i++) {
        var r = dataTray[i];
        if (r[0]) {
          trayList.push({
            id: r[0].toString(),
            nama: r[1] ? r[1].toString() : "",
            lebar: safeParse(r[2]),
            tinggi: safeParse(r[3]),
            maxLoad: safeParse(r[4])
          });
        }
      }
    }

    // 3. Ambil data History
    var sheetHistory = ss.getSheetByName("Simulasi_History");
    if (sheetHistory) {
      var dataHistory = sheetHistory.getDataRange().getValues();
      // Baca secara descending (terbaru di atas)
      for (var i = dataHistory.length - 1; i >= 1; i--) {
        if (dataHistory[i][0]) {
          historyList.push(mapHistoryRow(dataHistory[i], tz));
        }
      }
    }

    return {
      success: true,
      kabel: kabelList,
      tray: trayList,
      history: historyList
    };
  } catch (error) {
    return { success: false, error: error.toString() };
  }
}

/**
 * Mengambil satu simulasi lengkap dengan detail bundle kabel.
 * Endpoint utama yang dipakai plugin Revit:
 * GET ?action=getSimulation&id=SIM-20260717-0001
 */
function getSimulationById(simId) {
  try {
    if (!simId) return { success: false, error: "Parameter 'id' wajib diisi." };

    var ss = SpreadsheetApp.getActiveSpreadsheet();
    var tz = ss.getSpreadsheetTimeZone();
    var sheet = ss.getSheetByName("Simulasi_History");
    if (!sheet) return { success: false, error: "Sheet Simulasi_History tidak ditemukan." };

    var data = sheet.getDataRange().getValues();
    for (var i = 1; i < data.length; i++) {
      if (data[i][0] && data[i][0].toString() === simId) {
        return { success: true, simulation: mapHistoryRow(data[i], tz) };
      }
    }
    return { success: false, error: "Simulasi dengan ID " + simId + " tidak ditemukan." };
  } catch (error) {
    return { success: false, error: error.toString() };
  }
}

/**
 * Menghapus PERMANEN satu riwayat simulasi dari sheet Simulasi_History.
 * POST body: {action:"deleteSimulation", data:{id:"SIM-xxx"}}
 */
function deleteSimulation(data) {
  try {
    var simId = data && data.id ? data.id.toString().trim() : "";
    if (!simId) return { success: false, error: "Parameter 'id' wajib diisi." };

    var ss = SpreadsheetApp.getActiveSpreadsheet();
    var sheet = ss.getSheetByName("Simulasi_History");
    if (!sheet) return { success: false, error: "Sheet Simulasi_History tidak ditemukan." };

    var values = sheet.getDataRange().getValues();
    // Cari dari bawah agar aman bila ada ID ganda (hapus semuanya)
    var deleted = 0;
    for (var i = values.length - 1; i >= 1; i--) {
      if (values[i][0] && values[i][0].toString() === simId) {
        sheet.deleteRow(i + 1); // baris sheet 1-based, +1 karena header
        deleted++;
      }
    }
    SpreadsheetApp.flush();
    if (deleted === 0) {
      return { success: false, error: "Simulasi dengan ID " + simId + " tidak ditemukan." };
    }
    return { success: true, id: simId, deleted: deleted };
  } catch (error) {
    return { success: false, error: error.toString() };
  }
}

/**
 * Menyimpan simulasi baru ke Simulasi_History
 * (kolom ke-13 "Detail_JSON" menyimpan bundle kabel lengkap untuk Revit)
 */
function saveSimulation(simulation) {
  try {
    var ss = SpreadsheetApp.getActiveSpreadsheet();
    var sheet = ss.getSheetByName("Simulasi_History");
    if (!sheet) {
      return { success: false, error: "Sheet Simulasi_History tidak ditemukan." };
    }

    var now = new Date();
    var tz = ss.getSpreadsheetTimeZone();
    var timestamp = Utilities.formatDate(now, tz, "dd/MM/yyyy HH:mm:ss");

    // Auto-generate ID Simulasi
    var datePrefix = Utilities.formatDate(now, tz, "yyyyMMdd");
    var data = sheet.getDataRange().getValues();
    var nextNum = 1;
    for (var i = 1; i < data.length; i++) {
      var currentId = data[i][0];
      if (currentId && currentId.indexOf("SIM-" + datePrefix) === 0) {
        var parts = currentId.split("-");
        if (parts.length === 3) {
          var num = parseInt(parts[2], 10);
          if (num >= nextNum) nextNum = num + 1;
        }
      }
    }
    var simId = "SIM-" + datePrefix + "-" + ("0000" + nextNum).slice(-4);

    var detailJson = "";
    if (simulation.detail) {
      try { detailJson = JSON.stringify(simulation.detail); } catch (err) { detailJson = ""; }
    }

    sheet.appendRow([
      simId,
      now, // Disimpan sebagai objek tanggal agar mempermudah format spreadsheet asli
      simulation.namaProyek,
      simulation.jenisTray,
      simulation.lebarTray,
      simulation.metode,
      simulation.persenSpare,
      simulation.totalLebarKabel,
      simulation.totalBerat,
      simulation.status,
      simulation.rekomendasi,
      simulation.user,
      detailJson
    ]);

    SpreadsheetApp.flush();
    return { success: true, id: simId, timestamp: timestamp };
  } catch (error) {
    return { success: false, error: error.toString() };
  }
}

/**
 * ====================================================================
 * GERBANG LOGIN & MANAJEMEN USER
 * ====================================================================
 * Sumber data: sheet "Users" (dibuat oleh initUsers() di Setup.gs).
 * Password TIDAK PERNAH disimpan apa adanya — hanya SHA-256 bergaram
 * (salt acak per user).
 *
 * Endpoint (POST body {action, data}):
 *   login          {username, password}          -> {token, user, expiresAt, offline}
 *   logout         {token}
 *   listUsers      {token}                        (admin)
 *   addUser        {token, username, password, role}   (admin)
 *   deleteUser     {token, username}              (admin)
 *   setUserActive  {token, username, aktif}       (admin)
 *   changePassword {token, username, password}    (admin, atau user utk dirinya sendiri)
 * ====================================================================
 */

var SESSION_TTL_SEC = 21600; // 6 jam

function usersSheet_() {
  return SpreadsheetApp.getActiveSpreadsheet().getSheetByName("Users");
}

function sha256Hex_(str) {
  var raw = Utilities.computeDigest(
    Utilities.DigestAlgorithm.SHA_256, str, Utilities.Charset.UTF_8);
  var out = "";
  for (var i = 0; i < raw.length; i++) {
    out += ("0" + (raw[i] & 0xFF).toString(16)).slice(-2);
  }
  return out;
}

/** Hash password bergaram. Formula ini HARUS sama persis dengan sisi web. */
function hashPassword_(password, salt) {
  return sha256Hex_(salt + "|" + password);
}

/** Membaca seluruh user dari sheet (termasuk nomor baris untuk update). */
function readUsers_() {
  var sheet = usersSheet_();
  if (!sheet) return [];
  var values = sheet.getDataRange().getValues();
  var list = [];
  for (var i = 1; i < values.length; i++) {
    var r = values[i];
    if (!r[0]) continue;
    list.push({
      row: i + 1,
      username: r[0].toString(),
      hash: r[1] ? r[1].toString() : "",
      salt: r[2] ? r[2].toString() : "",
      role: r[3] ? r[3].toString() : "user",
      aktif: r[4] === "" || r[4] === null || r[4] === undefined ? true : (r[4] === true || r[4].toString().toLowerCase() === "true"),
      dibuat: r[5] || "",
      terakhirLogin: r[6] || ""
    });
  }
  return list;
}

function findUser_(username) {
  if (!username) return null;
  var target = username.toString().trim().toLowerCase();
  var users = readUsers_();
  for (var i = 0; i < users.length; i++) {
    if (users[i].username.toLowerCase() === target) return users[i];
  }
  return null;
}

/** Perbandingan hash yang tidak bocor lewat waktu eksekusi. */
function safeEquals_(a, b) {
  if (!a || !b || a.length !== b.length) return false;
  var diff = 0;
  for (var i = 0; i < a.length; i++) diff |= (a.charCodeAt(i) ^ b.charCodeAt(i));
  return diff === 0;
}

function apiLogin(data) {
  try {
    var username = data && data.username ? data.username.toString().trim() : "";
    var password = data && data.password ? data.password.toString() : "";
    if (!username || !password) {
      return { success: false, error: "Username dan password wajib diisi." };
    }

    var sheet = usersSheet_();
    if (!sheet) {
      return { success: false, error: "Sheet 'Users' belum ada. Jalankan initUsers() di Apps Script dahulu." };
    }

    var user = findUser_(username);
    // Pesan sengaja disamakan supaya tidak membocorkan username mana yang ada.
    var gagal = { success: false, error: "Username atau password salah." };
    if (!user) return gagal;
    if (!user.aktif) return { success: false, error: "Akun ini dinonaktifkan. Hubungi admin." };
    if (!safeEquals_(hashPassword_(password, user.salt), user.hash)) return gagal;

    var token = Utilities.getUuid().replace(/-/g, "") +
                Utilities.getUuid().replace(/-/g, "");
    var expiresAt = new Date().getTime() + SESSION_TTL_SEC * 1000;
    CacheService.getScriptCache().put(
      "sess_" + token,
      JSON.stringify({ u: user.username, r: user.role, exp: expiresAt }),
      SESSION_TTL_SEC);

    try {
      sheet.getRange(user.row, 7).setValue(new Date());
      SpreadsheetApp.flush();
    } catch (errLog) { /* pencatatan waktu login bukan hal kritis */ }

    return {
      success: true,
      token: token,
      expiresAt: expiresAt,
      user: { username: user.username, role: user.role },
      // Dipakai web untuk login OFFLINE di perangkat ini bila API tak terjangkau.
      offline: { salt: user.salt, hash: user.hash }
    };
  } catch (error) {
    return { success: false, error: error.toString() };
  }
}

function apiLogout(data) {
  try {
    var token = data && data.token ? data.token.toString() : "";
    if (token) CacheService.getScriptCache().remove("sess_" + token);
    return { success: true };
  } catch (error) {
    return { success: false, error: error.toString() };
  }
}

/** Mengembalikan {username, role} bila token masih sah, selain itu null. */
function sessionUser_(token) {
  if (!token) return null;
  var raw = CacheService.getScriptCache().get("sess_" + token.toString());
  if (!raw) return null;
  try {
    var s = JSON.parse(raw);
    if (!s || !s.exp || s.exp < new Date().getTime()) return null;
    return { username: s.u, role: s.r };
  } catch (err) {
    return null;
  }
}

function requireAdmin_(data) {
  var sess = sessionUser_(data && data.token);
  if (!sess) return { ok: false, res: { success: false, error: "Sesi berakhir. Silakan login ulang.", expired: true } };
  if (sess.role !== "admin") return { ok: false, res: { success: false, error: "Hanya admin yang boleh melakukan ini." } };
  return { ok: true, sess: sess };
}

function apiListUsers(data) {
  try {
    var guard = requireAdmin_(data);
    if (!guard.ok) return guard.res;

    var ss = SpreadsheetApp.getActiveSpreadsheet();
    var tz = ss.getSpreadsheetTimeZone();
    var users = readUsers_().map(function (u) {
      var last = "";
      if (u.terakhirLogin instanceof Date) {
        last = Utilities.formatDate(u.terakhirLogin, tz, "dd/MM/yyyy HH:mm");
      } else if (u.terakhirLogin) {
        last = u.terakhirLogin.toString();
      }
      // Hash & salt sengaja TIDAK dikirim ke daftar user.
      return { username: u.username, role: u.role, aktif: u.aktif, terakhirLogin: last };
    });
    return { success: true, users: users };
  } catch (error) {
    return { success: false, error: error.toString() };
  }
}

function apiAddUser(data) {
  try {
    var guard = requireAdmin_(data);
    if (!guard.ok) return guard.res;

    var username = data.username ? data.username.toString().trim() : "";
    var password = data.password ? data.password.toString() : "";
    var role = data.role === "admin" ? "admin" : "user";

    if (!username || !password) return { success: false, error: "Username dan password wajib diisi." };
    if (!/^[A-Za-z0-9._-]{3,30}$/.test(username)) {
      return { success: false, error: "Username 3-30 karakter, hanya huruf/angka/titik/garis." };
    }
    if (password.length < 4) return { success: false, error: "Password minimal 4 karakter." };
    if (findUser_(username)) return { success: false, error: "Username '" + username + "' sudah dipakai." };

    var sheet = usersSheet_();
    if (!sheet) return { success: false, error: "Sheet 'Users' belum ada. Jalankan initUsers() dahulu." };

    var salt = Utilities.getUuid().replace(/-/g, "");
    sheet.appendRow([username, hashPassword_(password, salt), salt, role, true, new Date(), ""]);
    SpreadsheetApp.flush();
    return { success: true, username: username, role: role };
  } catch (error) {
    return { success: false, error: error.toString() };
  }
}

function apiDeleteUser(data) {
  try {
    var guard = requireAdmin_(data);
    if (!guard.ok) return guard.res;

    var username = data.username ? data.username.toString().trim() : "";
    if (!username) return { success: false, error: "Username wajib diisi." };
    if (username.toLowerCase() === guard.sess.username.toLowerCase()) {
      return { success: false, error: "Tidak bisa menghapus akun yang sedang dipakai." };
    }

    var user = findUser_(username);
    if (!user) return { success: false, error: "User '" + username + "' tidak ditemukan." };

    // Cegah kehilangan admin terakhir supaya tidak terkunci dari dashboard.
    if (user.role === "admin" && countActiveAdmins_() <= 1) {
      return { success: false, error: "Ini admin aktif terakhir — tidak boleh dihapus." };
    }

    usersSheet_().deleteRow(user.row);
    SpreadsheetApp.flush();
    return { success: true, username: username };
  } catch (error) {
    return { success: false, error: error.toString() };
  }
}

function countActiveAdmins_() {
  var users = readUsers_();
  var n = 0;
  for (var i = 0; i < users.length; i++) {
    if (users[i].role === "admin" && users[i].aktif) n++;
  }
  return n;
}

function apiSetUserActive(data) {
  try {
    var guard = requireAdmin_(data);
    if (!guard.ok) return guard.res;

    var username = data.username ? data.username.toString().trim() : "";
    var aktif = data.aktif === true || data.aktif === "true";
    var user = findUser_(username);
    if (!user) return { success: false, error: "User '" + username + "' tidak ditemukan." };
    if (username.toLowerCase() === guard.sess.username.toLowerCase() && !aktif) {
      return { success: false, error: "Tidak bisa menonaktifkan akun yang sedang dipakai." };
    }
    if (!aktif && user.role === "admin" && countActiveAdmins_() <= 1) {
      return { success: false, error: "Ini admin aktif terakhir — tidak boleh dinonaktifkan." };
    }

    usersSheet_().getRange(user.row, 5).setValue(aktif);
    SpreadsheetApp.flush();
    return { success: true, username: user.username, aktif: aktif };
  } catch (error) {
    return { success: false, error: error.toString() };
  }
}

function apiChangePassword(data) {
  try {
    var sess = sessionUser_(data && data.token);
    if (!sess) return { success: false, error: "Sesi berakhir. Silakan login ulang.", expired: true };

    var username = data.username ? data.username.toString().trim() : sess.username;
    var password = data.password ? data.password.toString() : "";
    if (password.length < 4) return { success: false, error: "Password minimal 4 karakter." };

    // User biasa hanya boleh mengganti password miliknya sendiri.
    if (sess.role !== "admin" && username.toLowerCase() !== sess.username.toLowerCase()) {
      return { success: false, error: "Hanya admin yang boleh mengganti password user lain." };
    }

    var user = findUser_(username);
    if (!user) return { success: false, error: "User '" + username + "' tidak ditemukan." };

    var salt = Utilities.getUuid().replace(/-/g, "");
    var sheet = usersSheet_();
    sheet.getRange(user.row, 2).setValue(hashPassword_(password, salt));
    sheet.getRange(user.row, 3).setValue(salt);
    SpreadsheetApp.flush();
    return { success: true, username: user.username };
  } catch (error) {
    return { success: false, error: error.toString() };
  }
}
