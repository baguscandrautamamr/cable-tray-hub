/**
 * ====================================================================
 * CABLE TRAY PROJECT HUB - SETUP SCRIPT (132 CABLES ULTRA EDITION)
 * ====================================================================
 * Script inisialisasi awal untuk membangun tabel database di Google Sheets.
 * Menghasilkan sheet Katalog_Kabel, Tray_Templates, dan Simulasi_History.
 * Seluruh data kabel diekstrak komprehensif dari dokumen standar KMI.
 * ====================================================================
 */

function initSetup() {
  var ss = SpreadsheetApp.getActiveSpreadsheet();
  
  // 1. Inisialisasi Sheet "Katalog_Kabel"
  var sheetKabel = ss.getSheetByName("Katalog_Kabel");
  if (!sheetKabel) {
    sheetKabel = ss.insertSheet("Katalog_Kabel");
  }
  sheetKabel.clear();
  var headerKabel = ["ID_Kabel", "Nama_Kabel", "Tipe_Core", "Diameter_De_mm", "Max_Current_A", "Berat_kg_m"];
  sheetKabel.appendRow(headerKabel);
  sheetKabel.getRange(1, 1, 1, headerKabel.length).setFontWeight("bold").setBackground("#FF9800").setFontColor("#FFFFFF");
  
  // Data Master Kabel Premium KMI (KMI Kabelmetal Standard)
  var defaultCables = [
    ["KAB-0001", "NYA 1.5 mm²", "Single Core", 3.1, 24, 0.022],
    ["KAB-0002", "NYA 2.5 mm²", "Single Core", 3.7, 32, 0.034],
    ["KAB-0003", "NYA 4.0 mm²", "Single Core", 4.3, 42, 0.050],
    ["KAB-0004", "NYA 6.0 mm²", "Single Core", 4.8, 54, 0.070],
    ["KAB-0005", "NYA 10.0 mm²", "Single Core", 6.2, 73, 0.117],
    ["KAB-0006", "NYA 16.0 mm²", "Single Core", 7.2, 98, 0.173],
    ["KAB-0007", "NYA 25.0 mm²", "Single Core", 9.0, 129, 0.277],
    ["KAB-0008", "NYA 35.0 mm²", "Single Core", 10.1, 158, 0.369],
    ["KAB-0009", "NYA 50.0 mm²", "Single Core", 12.1, 197, 0.513],
    ["KAB-0010", "NYA 70.0 mm²", "Single Core", 13.8, 245, 0.709],
    ["KAB-0011", "NYA 95.0 mm²", "Single Core", 16.0, 290, 0.958],
    ["KAB-0012", "NYA 120.0 mm²", "Single Core", 17.6, 345, 1.183],
    ["KAB-0013", "NYA 150.0 mm²", "Single Core", 19.5, 390, 1.448],
    ["KAB-0014", "NYA 185.0 mm²", "Single Core", 22.0, 445, 1.835],
    ["KAB-0015", "NYA 240.0 mm²", "Single Core", 25.5, 525, 2.413],
    ["KAB-0016", "NYA 300.0 mm²", "Single Core", 28.0, 605, 2.958],
    ["KAB-0017", "NYA 400.0 mm²", "Single Core", 32.5, 715, 3.782],
    ["KAB-0018", "NYM 2x1.5 mm²", "Multi Core", 9.5, 19, 0.116],
    ["KAB-0019", "NYM 2x2.5 mm²", "Multi Core", 10.5, 25, 0.157],
    ["KAB-0020", "NYM 2x4.0 mm²", "Multi Core", 11.5, 34, 0.203],
    ["KAB-0021", "NYM 2x6.0 mm²", "Multi Core", 12.5, 44, 0.262],
    ["KAB-0022", "NYM 2x10.0 mm²", "Multi Core", 16.0, 60, 0.426],
    ["KAB-0023", "NYM 2x16.0 mm²", "Multi Core", 19.0, 80, 0.638],
    ["KAB-0024", "NYM 2x25.0 mm²", "Multi Core", 23.0, 110, 0.962],
    ["KAB-0025", "NYM 2x35.0 mm²", "Multi Core", 26.0, 135, 1.270],
    ["KAB-0026", "NYY 1x1.5 mm²", "Single Core", 6.1, 27, 0.053],
    ["KAB-0027", "NYY 1x2.5 mm²", "Single Core", 6.6, 36, 0.067],
    ["KAB-0028", "NYY 1x4.0 mm²", "Single Core", 7.6, 47, 0.094],
    ["KAB-0029", "NYY 1x6.0 mm²", "Single Core", 8.1, 59, 0.117],
    ["KAB-0030", "NYY 1x10.0 mm²", "Single Core", 9.1, 77, 0.166],
    ["KAB-0031", "NYY 1x16.0 mm²", "Single Core", 10.1, 101, 0.229],
    ["KAB-0032", "NYY 1x25.0 mm²", "Single Core", 11.9, 135, 0.345],
    ["KAB-0033", "NYY 1x35.0 mm²", "Single Core", 13.0, 163, 0.444],
    ["KAB-0034", "NYY 1x50.0 mm²", "Single Core", 15.0, 198, 0.600],
    ["KAB-0035", "NYY 1x70.0 mm²", "Single Core", 16.9, 251, 0.815],
    ["KAB-0036", "NYY 1x95.0 mm²", "Single Core", 19.1, 308, 1.079],
    ["KAB-0037", "NYY 1x120.0 mm²", "Single Core", 21.0, 359, 1.325],
    ["KAB-0038", "NYY 1x150.0 mm²", "Single Core", 23.0, 414, 1.604],
    ["KAB-0039", "NYY 1x185.0 mm²", "Single Core", 25.5, 475, 2.020],
    ["KAB-0040", "NYY 1x240.0 mm²", "Single Core", 29.0, 564, 2.636],
    ["KAB-0041", "NYY 1x300.0 mm²", "Single Core", 32.0, 651, 3.219],
    ["KAB-0042", "NYY 1x400.0 mm²", "Single Core", 35.5, 752, 4.087],
    ["KAB-0043", "NYY 1x500.0 mm²", "Single Core", 39.5, 868, 5.213],
    ["KAB-0044", "NYY 1x630.0 mm²", "Single Core", 44.0, 1005, 6.712],
    ["KAB-0045", "NYY 1x800.0 mm²", "Single Core", 48.5, 1140, 8.368],
    ["KAB-0046", "NYBY 1x16 mm²", "Single Core", 15.4, 102, 0.396],
    ["KAB-0047", "NYBY 1x25 mm²", "Single Core", 16.2, 124, 0.497],
    ["KAB-0048", "NYBY 1x35 mm²", "Single Core", 17.3, 149, 0.608],
    ["KAB-0049", "NYBY 1x50 mm²", "Single Core", 19.3, 175, 0.785],
    ["KAB-0050", "NYBY 1x70 mm²", "Single Core", 21.5, 216, 1.011],
    ["KAB-0051", "NYBY 1x95 mm²", "Single Core", 23.5, 258, 1.298],
    ["KAB-0052", "NYBY 1x120 mm²", "Single Core", 25.0, 299, 1.552],
    ["KAB-0053", "NYBY 1x150 mm²", "Single Core", 27.0, 342, 1.850],
    ["KAB-0054", "NYBY 1x185 mm²", "Single Core", 29.5, 396, 2.279],
    ["KAB-0055", "NYBY 1x240 mm²", "Single Core", 32.5, 469, 2.926],
    ["KAB-0056", "NYBY 1x300 mm²", "Single Core", 35.5, 538, 3.521],
    ["KAB-0057", "NYBY 1x400 mm²", "Single Core", 39.5, 622, 4.476],
    ["KAB-0058", "NYBY 1x500 mm²", "Single Core", 43.5, 715, 5.645],
    ["KAB-0059", "NYBY 1x630 mm²", "Single Core", 48.0, 810, 7.190],
    ["KAB-0060", "NYBY 1x800 mm²", "Single Core", 54.0, 920, 8.941],
    ["KAB-0061", "NYCY 1x1.5/1.5 mm²", "Single Core", 9.9, 28, 0.121],
    ["KAB-0062", "NYCY 1x2.5/2.5 mm²", "Single Core", 10.4, 37, 0.143],
    ["KAB-0063", "NYCY 1x4/4 mm²", "Single Core", 11.4, 48, 0.188],
    ["KAB-0064", "NYCY 1x6/6 mm²", "Single Core", 11.9, 60, 0.233],
    ["KAB-0065", "NYCY 1x10/10 mm²", "Single Core", 12.9, 79, 0.324],
    ["KAB-0066", "NYCY 1x16/16 mm²", "Single Core", 14.4, 103, 0.456],
    ["KAB-0067", "NYCY 1x25/16 mm²", "Single Core", 16.1, 137, 0.582],
    ["KAB-0068", "NYCY 1x35/16 mm²", "Single Core", 17.2, 165, 0.687],
    ["KAB-0069", "NYCY 1x50/25 mm²", "Single Core", 19.7, 200, 0.943],
    ["KAB-0070", "NYCY 1x70/35 mm²", "Single Core", 22.0, 253, 1.259],
    ["KAB-0071", "NYCY 1x95/50 mm²", "Single Core", 24.5, 310, 1.678],
    ["KAB-0072", "NYCY 1x120/70 mm²", "Single Core", 27.0, 361, 2.122],
    ["KAB-0073", "NYCY 1x150/70 mm²", "Single Core", 28.5, 414, 2.410],
    ["KAB-0074", "NYCY 1x185/95 mm²", "Single Core", 31.5, 478, 3.049],
    ["KAB-0075", "NYCY 1x240/120 mm²", "Single Core", 35.0, 567, 3.898],
    ["KAB-0076", "NYCY 1x300/150 mm²", "Single Core", 38.0, 654, 4.778],
    ["KAB-0077", "NYCY 1x400/185 mm²", "Single Core", 43.0, 755, 6.031],
    ["KAB-0078", "NYCY 1x500/240 mm²", "Single Core", 47.5, 871, 7.689],
    ["KAB-0079", "NYCY 1x630/300 mm²", "Single Core", 52.5, 1008, 9.778],
    ["KAB-0080", "NYCY 1x800/400 mm²", "Single Core", 58.5, 1143, 12.427],
    ["KAB-0081", "NYFGbY 2x1.5 mm²", "Multi Core", 17.9, 28, 0.611],
    ["KAB-0082", "NYFGbY 2x2.5 mm²", "Multi Core", 18.0, 32, 0.630],
    ["KAB-0083", "NYFGbY 2x4.0 mm²", "Multi Core", 17.8, 43, 0.648],
    ["KAB-0084", "NYFGbY 2x6.0 mm²", "Multi Core", 18.1, 55, 0.687],
    ["KAB-0085", "NYFGbY 2x10.0 mm²", "Multi Core", 20.5, 75, 0.881],
    ["KAB-0086", "NYFGbY 2x16.0 mm²", "Multi Core", 22.5, 95, 1.085],
    ["KAB-0087", "NYFGbY 2x25.0 mm²", "Multi Core", 25.5, 125, 1.428],
    ["KAB-0088", "NYFGbY 2x35.0 mm²", "Multi Core", 27.5, 150, 1.753],
    ["KAB-0089", "NYFGbY 2x50.0 mm²", "Multi Core", 31.0, 185, 2.120],
    ["KAB-0090", "NYFGbY 2x70.0 mm²", "Multi Core", 35.0, 230, 2.716],
    ["KAB-0091", "NYFGbY 2x95.0 mm²", "Multi Core", 39.5, 280, 3.528],
    ["KAB-0092", "NYFGbY 2x120.0 mm²", "Multi Core", 43.0, 320, 4.177],
    ["KAB-0093", "NYFGbY 2x150.0 mm²", "Multi Core", 47.0, 365, 5.012],
    ["KAB-0094", "NYFGbY 2x185.0 mm²", "Multi Core", 52.0, 420, 6.116],
    ["KAB-0095", "NYFGbY 2x240.0 mm²", "Multi Core", 58.0, 490, 7.695],
    ["KAB-0096", "NYFGbY 2x300.0 mm²", "Multi Core", 64.0, 560, 9.384],
    ["KAB-0097", "NYRY 1x10 mm²", "Single Core", 14.4, 75, 0.326],
    ["KAB-0098", "NYRY 1x16 mm²", "Single Core", 15.4, 98, 0.402],
    ["KAB-0099", "NYRY 1x25 mm²", "Single Core", 17.2, 130, 0.541],
    ["KAB-0100", "NYRY 1x35 mm²", "Single Core", 18.3, 155, 0.658],
    ["KAB-0101", "NYRY 1x50 mm²", "Single Core", 20.5, 182, 0.841],
    ["KAB-0102", "NYRY 1x70 mm²", "Single Core", 22.5, 225, 1.070],
    ["KAB-0103", "NYRY 1x95 mm²", "Single Core", 24.5, 270, 1.365],
    ["KAB-0104", "NYRY 1x120 mm²", "Single Core", 26.5, 310, 1.662],
    ["KAB-0105", "NYRY 1x150 mm²", "Single Core", 28.5, 355, 1.967],
    ["KAB-0106", "NYRY 1x185 mm²", "Single Core", 31.0, 410, 2.406],
    ["KAB-0107", "NYRY 1x240 mm²", "Single Core", 34.0, 485, 3.071],
    ["KAB-0108", "NYRY 1x300 mm²", "Single Core", 38.0, 555, 3.781],
    ["KAB-0109", "NYRY 1x400 mm²", "Single Core", 42.0, 640, 4.764],
    ["KAB-0110", "NYRY 1x500 mm²", "Single Core", 46.5, 730, 5.968],
    ["KAB-0111", "NYRY 1x630 mm²", "Single Core", 51.0, 825, 7.538],
    ["KAB-0112", "NYRY 1x800 mm²", "Single Core", 57.0, 935, 9.508],
    ["KAB-0113", "NYSY 1x1.5 mm²", "Single Core", 8.8, 28, 0.110],
    ["KAB-0114", "NYSY 1x2.5 mm²", "Single Core", 9.3, 36, 0.128],
    ["KAB-0115", "NYSY 1x4.0 mm²", "Single Core", 10.2, 48, 0.162],
    ["KAB-0116", "NYSY 1x6.0 mm²", "Single Core", 10.8, 59, 0.191],
    ["KAB-0117", "NYSY 1x10.0 mm²", "Single Core", 11.7, 78, 0.247],
    ["KAB-0118", "NYSY 1x16.0 mm²", "Single Core", 12.7, 102, 0.318],
    ["KAB-0119", "NYSY 1x25.0 mm²", "Single Core", 14.6, 136, 0.449],
    ["KAB-0120", "NYSY 1x35.0 mm²", "Single Core", 15.7, 164, 0.558],
    ["KAB-0121", "NYSY 1x50.0 mm²", "Single Core", 17.6, 199, 0.730],
    ["KAB-0122", "NYSY 1x70.0 mm²", "Single Core", 19.4, 252, 0.951],
    ["KAB-0123", "NYSY 1x95.0 mm²", "Single Core", 22.0, 309, 1.232],
    ["KAB-0124", "NYSY 1x120.0 mm²", "Single Core", 23.5, 360, 1.481],
    ["KAB-0125", "NYSY 1x150.0 mm²", "Single Core", 25.5, 413, 1.774],
    ["KAB-0126", "NYSY 1x185.0 mm²", "Single Core", 27.5, 476, 2.197],
    ["KAB-0127", "NYSY 1x240.0 mm²", "Single Core", 31.0, 565, 2.821],
    ["KAB-0128", "NYSY 1x300.0 mm²", "Single Core", 34.0, 652, 3.422],
    ["KAB-0129", "NYSY 1x400.0 mm²", "Single Core", 38.0, 753, 4.350],
    ["KAB-0130", "NYSY 1x500.0 mm²", "Single Core", 42.0, 869, 5.504],
    ["KAB-0131", "NYSY 1x630.0 mm²", "Single Core", 46.5, 1006, 7.035],
    ["KAB-0132", "NYSY 1x800.0 mm²", "Single Core", 52.0, 1141, 8.770]
  ];
  sheetKabel.getRange(2, 1, defaultCables.length, defaultCables[0].length).setValues(defaultCables);
  
  // 2. Inisialisasi Sheet "Tray_Templates"
  var sheetTray = ss.getSheetByName("Tray_Templates");
  if (!sheetTray) {
    sheetTray = ss.insertSheet("Tray_Templates");
  }
  sheetTray.clear();
  var headerTray = ["ID_Template", "Nama_Template", "Lebar_mm", "Tinggi_mm", "Max_Load_kg_m"];
  sheetTray.appendRow(headerTray);
  sheetTray.getRange(1, 1, 1, headerTray.length).setFontWeight("bold").setBackground("#FF9800").setFontColor("#FFFFFF");
  
  var defaultTray = [
    ["TRY-0001", "Tray 100x100 mm", 100, 100, 20.0],
    ["TRY-0002", "Tray 200x100 mm", 200, 100, 40.0],
    ["TRY-0003", "Tray 300x100 mm", 300, 100, 60.0],
    ["TRY-0004", "Tray 400x100 mm", 400, 100, 80.0],
    ["TRY-0005", "Tray 500x100 mm", 500, 100, 100.0],
    ["TRY-0006", "Tray 600x100 mm", 600, 100, 120.0],
    ["TRY-0007", "Tray 800x100 mm", 800, 100, 160.0],
    ["TRY-0008", "Tray 1000x100 mm", 1000, 100, 200.0],
    ["TRY-0009", "Tray 1200x100 mm", 1200, 100, 240.0]
  ];
  sheetTray.getRange(2, 1, defaultTray.length, defaultTray[0].length).setValues(defaultTray);

  // 3. Inisialisasi Sheet "Simulasi_History"
  var sheetHistory = ss.getSheetByName("Simulasi_History");
  if (!sheetHistory) {
    sheetHistory = ss.insertSheet("Simulasi_History");
  }
  sheetHistory.clear();
  var headerHistory = [
    "ID_Simulasi", "Tanggal", "Nama_Proyek", "Jenis_Tray", "Lebar_Tray_mm",
    "Metode_Penataan", "Persen_Spare", "Total_Lebar_Kabel_mm",
    "Total_Berat_kg_m", "Status_Muat", "Rekomendasi_Tray", "Oleh_User", "Detail_JSON"
  ];
  sheetHistory.appendRow(headerHistory);
  sheetHistory.getRange(1, 1, 1, headerHistory.length).setFontWeight("bold").setBackground("#FF9800").setFontColor("#FFFFFF");

  // Masukkan riwayat default perdana
  var defaultHistory = [
    ["SIM-20260610-0001", new Date(), "Gedung Kantor Sudirman", "Perforated", 300, "Flat Touching", "20%", 180, 24.5, "AMAN", "-", "Admin Utama", ""]
  ];
  sheetHistory.getRange(2, 1, defaultHistory.length, defaultHistory[0].length).setValues(defaultHistory);

  SpreadsheetApp.flush();
}

/**
 * ====================================================================
 * UPGRADE UNTUK SPREADSHEET LAMA (tanpa menghapus data yang sudah ada)
 * ====================================================================
 * Jalankan fungsi ini SEKALI jika spreadsheet Anda sudah berisi data
 * dan Anda tidak ingin menjalankan ulang initSetup() (yang menghapus data).
 * Fungsi ini hanya menambahkan kolom "Detail_JSON" ke Simulasi_History
 * bila kolom tersebut belum ada.
 */
function upgradeSheets() {
  var ss = SpreadsheetApp.getActiveSpreadsheet();
  var sheetHistory = ss.getSheetByName("Simulasi_History");
  if (!sheetHistory) {
    throw new Error("Sheet Simulasi_History tidak ditemukan. Jalankan initSetup() terlebih dahulu.");
  }

  var lastCol = sheetHistory.getLastColumn();
  var headers = sheetHistory.getRange(1, 1, 1, lastCol).getValues()[0];
  var hasDetail = false;
  for (var i = 0; i < headers.length; i++) {
    if (headers[i] && headers[i].toString() === "Detail_JSON") {
      hasDetail = true;
      break;
    }
  }

  if (!hasDetail) {
    var newCol = lastCol + 1;
    sheetHistory.getRange(1, newCol)
      .setValue("Detail_JSON")
      .setFontWeight("bold").setBackground("#FF9800").setFontColor("#FFFFFF");
    SpreadsheetApp.flush();
  }
}