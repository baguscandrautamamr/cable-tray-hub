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
