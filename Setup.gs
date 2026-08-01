/**
 * ====================================================================
 * CABLE TRAY PROJECT HUB - SETUP SCRIPT (185 CABLES - IDENTIK DENGAN WEBSITE)
 * ====================================================================
 * Script inisialisasi awal untuk membangun tabel database di Google Sheets.
 * Menghasilkan sheet Katalog_Kabel, Tray_Templates, dan Simulasi_History.
 * Data kabel = 185 item, diekstrak langsung dari katalog website (KMI).
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
    ["KAB-0001", "NYA 1x1.5 mm²", "Single Core", 3.1, 24, 0.022],
    ["KAB-0002", "NYA 1x2.5 mm²", "Single Core", 3.7, 32, 0.034],
    ["KAB-0003", "NYA 1x4 mm²", "Single Core", 4.3, 42, 0.05],
    ["KAB-0004", "NYA 1x6 mm²", "Single Core", 4.8, 54, 0.07],
    ["KAB-0005", "NYA 1x10 mm²", "Single Core", 6.2, 73, 0.117],
    ["KAB-0006", "NYA 1x16 mm²", "Single Core", 7.2, 98, 0.173],
    ["KAB-0007", "NYA 1x25 mm²", "Single Core", 9, 129, 0.277],
    ["KAB-0008", "NYA 1x35 mm²", "Single Core", 10.1, 158, 0.369],
    ["KAB-0009", "NYA 1x50 mm²", "Single Core", 12.1, 197, 0.513],
    ["KAB-0010", "NYA 1x70 mm²", "Single Core", 13.8, 245, 0.709],
    ["KAB-0011", "NYA 1x95 mm²", "Single Core", 16, 290, 0.958],
    ["KAB-0012", "NYA 1x120 mm²", "Single Core", 17.6, 345, 1.183],
    ["KAB-0013", "NYA 1x150 mm²", "Single Core", 19.5, 390, 1.448],
    ["KAB-0014", "NYA 1x185 mm²", "Single Core", 22, 445, 1.835],
    ["KAB-0015", "NYA 1x240 mm²", "Single Core", 25.5, 525, 2.413],
    ["KAB-0016", "NYA 1x300 mm²", "Single Core", 28, 605, 2.958],
    ["KAB-0017", "NYA 1x400 mm²", "Single Core", 31.5, 715, 3.782],
    ["KAB-0018", "NYM 2x1.5 mm²", "Multi Core", 9.5, 0, 0.116],
    ["KAB-0019", "NYM 2x2.5 mm²", "Multi Core", 10.5, 0, 0.157],
    ["KAB-0020", "NYM 2x4 mm²", "Multi Core", 11.5, 0, 0.203],
    ["KAB-0021", "NYM 2x6 mm²", "Multi Core", 12.5, 0, 0.262],
    ["KAB-0022", "NYM 2x10 mm²", "Multi Core", 16, 0, 0.426],
    ["KAB-0023", "NYM 2x16 mm²", "Multi Core", 19, 0, 0.638],
    ["KAB-0024", "NYM 2x25 mm²", "Multi Core", 23, 0, 0.962],
    ["KAB-0025", "NYM 2x35 mm²", "Multi Core", 26, 0, 1.27],
    ["KAB-0026", "NYM 3x1.5 mm²", "Multi Core", 10, 0, 0.136],
    ["KAB-0027", "NYM 3x2.5 mm²", "Multi Core", 11, 0, 0.186],
    ["KAB-0028", "NYM 3x4 mm²", "Multi Core", 12, 0, 0.246],
    ["KAB-0029", "NYM 3x6 mm²", "Multi Core", 13.5, 0, 0.335],
    ["KAB-0030", "NYM 3x10 mm²", "Multi Core", 17, 0, 0.527],
    ["KAB-0031", "NYM 3x16 mm²", "Multi Core", 20.5, 0, 0.816],
    ["KAB-0032", "NYM 3x25 mm²", "Multi Core", 24.5, 0, 1.229],
    ["KAB-0033", "NYM 3x35 mm²", "Multi Core", 27.5, 0, 1.601],
    ["KAB-0034", "NYM 4x1.5 mm²", "Multi Core", 10.5, 0, 0.161],
    ["KAB-0035", "NYM 4x2.5 mm²", "Multi Core", 12, 0, 0.224],
    ["KAB-0036", "NYM 4x4 mm²", "Multi Core", 13.5, 0, 0.311],
    ["KAB-0037", "NYM 4x6 mm²", "Multi Core", 15.5, 0, 0.424],
    ["KAB-0038", "NYM 4x10 mm²", "Multi Core", 18.5, 0, 0.648],
    ["KAB-0039", "NYM 4x16 mm²", "Multi Core", 22.5, 0, 1.027],
    ["KAB-0040", "NYM 4x25 mm²", "Multi Core", 27.5, 0, 1.579],
    ["KAB-0041", "NYM 4x35 mm²", "Multi Core", 30, 0, 2.026],
    ["KAB-0042", "NYM 5x1.5 mm²", "Multi Core", 11.5, 0, 0.198],
    ["KAB-0043", "NYM 5x2.5 mm²", "Multi Core", 13, 0, 0.278],
    ["KAB-0044", "NYM 5x4 mm²", "Multi Core", 15, 0, 0.4],
    ["KAB-0045", "NYM 5x6 mm²", "Multi Core", 16.5, 0, 0.524],
    ["KAB-0046", "NYM 5x10 mm²", "Multi Core", 20, 0, 0.805],
    ["KAB-0047", "NYM 5x16 mm²", "Multi Core", 25, 0, 1.274],
    ["KAB-0048", "NYM 5x25 mm²", "Multi Core", 30, 0, 1.927],
    ["KAB-0049", "NYM 5x35 mm²", "Multi Core", 33.5, 0, 2.514],
    ["KAB-0050", "NYY 1x1.5 mm²", "Single Core", 6.1, 0, 0.053],
    ["KAB-0051", "NYY 1x2.5 mm²", "Single Core", 6.6, 0, 0.067],
    ["KAB-0052", "NYY 1x4 mm²", "Single Core", 7.6, 0, 0.094],
    ["KAB-0053", "NYY 1x6 mm²", "Single Core", 8.1, 0, 0.117],
    ["KAB-0054", "NYY 1x10 mm²", "Single Core", 9.1, 0, 0.166],
    ["KAB-0055", "NYY 1x16 mm²", "Single Core", 10.1, 0, 0.229],
    ["KAB-0056", "NYY 1x25 mm²", "Single Core", 11.9, 0, 0.345],
    ["KAB-0057", "NYY 1x35 mm²", "Single Core", 13, 0, 0.444],
    ["KAB-0058", "NYY 1x50 mm²", "Single Core", 15, 0, 0.6],
    ["KAB-0059", "NYY 1x70 mm²", "Single Core", 16.9, 0, 0.815],
    ["KAB-0060", "NYY 1x95 mm²", "Single Core", 19.1, 0, 1.079],
    ["KAB-0061", "NYY 1x120 mm²", "Single Core", 21, 0, 1.325],
    ["KAB-0062", "NYY 1x150 mm²", "Single Core", 23, 0, 1.604],
    ["KAB-0063", "NYY 1x185 mm²", "Single Core", 25.5, 0, 2.02],
    ["KAB-0064", "NYY 1x240 mm²", "Single Core", 29, 0, 2.636],
    ["KAB-0065", "NYY 1x300 mm²", "Single Core", 32, 0, 3.219],
    ["KAB-0066", "NYY 1x400 mm²", "Single Core", 35.5, 0, 4.087],
    ["KAB-0067", "NYY 1x500 mm²", "Single Core", 39.5, 0, 5.213],
    ["KAB-0068", "NYY 1x630 mm²", "Single Core", 44, 0, 6.712],
    ["KAB-0069", "NYY 1x800 mm²", "Single Core", 48.5, 0, 8.368],
    ["KAB-0070", "NYY 2x1.5 mm²", "Multi Core", 12.5, 0, 0.2],
    ["KAB-0071", "NYY 2x2.5 mm²", "Multi Core", 13.4, 0, 0.242],
    ["KAB-0072", "NYY 2x4 mm²", "Multi Core", 15.4, 0, 0.33],
    ["KAB-0073", "NYY 2x6 mm²", "Multi Core", 16.5, 0, 0.399],
    ["KAB-0074", "NYY 2x10 mm²", "Multi Core", 18.4, 0, 0.538],
    ["KAB-0075", "NYY 2x16 mm²", "Multi Core", 20.5, 0, 0.713],
    ["KAB-0076", "NYY 2x25 mm²", "Multi Core", 24, 0, 1.001],
    ["KAB-0077", "NYY 2x35 mm²", "Multi Core", 26, 0, 1.274],
    ["KAB-0078", "NYY 2x50 mm²", "Multi Core", 29.5, 0, 1.536],
    ["KAB-0079", "NYY 2x70 mm²", "Multi Core", 33, 0, 2.066],
    ["KAB-0080", "NYY 2x95 mm²", "Multi Core", 37.5, 0, 2.787],
    ["KAB-0081", "NYY 2x120 mm²", "Multi Core", 41, 0, 3.371],
    ["KAB-0082", "NYY 2x150 mm²", "Multi Core", 45, 0, 4.114],
    ["KAB-0083", "NYY 2x185 mm²", "Multi Core", 50, 0, 5.128],
    ["KAB-0084", "NYY 2x240 mm²", "Multi Core", 56, 0, 6.581],
    ["KAB-0085", "NYY 2x300 mm²", "Multi Core", 62, 0, 8.13],
    ["KAB-0086", "NYY 3x1.5 mm²", "Multi Core", 13, 0, 0.224],
    ["KAB-0087", "NYY 3x2.5 mm²", "Multi Core", 14, 0, 0.277],
    ["KAB-0088", "NYY 3x4 mm²", "Multi Core", 16.1, 0, 0.383],
    ["KAB-0089", "NYY 3x6 mm²", "Multi Core", 17.3, 0, 0.471],
    ["KAB-0090", "NYY 3x10 mm²", "Multi Core", 19.4, 0, 0.649],
    ["KAB-0091", "NYY 3x16 mm²", "Multi Core", 22, 0, 0.875],
    ["KAB-0092", "NYY 3x25 mm²", "Multi Core", 25, 0, 1.248],
    ["KAB-0093", "NYY 3x35 mm²", "Multi Core", 27.5, 0, 1.606],
    ["KAB-0094", "NYY 3x50 mm²", "Multi Core", 30, 0, 1.857],
    ["KAB-0095", "NYY 3x70 mm²", "Multi Core", 34, 0, 2.556],
    ["KAB-0096", "NYY 3x95 mm²", "Multi Core", 38.5, 0, 3.428],
    ["KAB-0097", "NYY 3x120 mm²", "Multi Core", 41.5, 0, 4.152],
    ["KAB-0098", "NYY 3x150 mm²", "Multi Core", 46, 0, 5.115],
    ["KAB-0099", "NYY 3x185 mm²", "Multi Core", 50.5, 0, 6.33],
    ["KAB-0100", "NYY 3x240 mm²", "Multi Core", 57, 0, 8.215],
    ["KAB-0101", "NYY 3x300 mm²", "Multi Core", 62.5, 0, 10.116],
    ["KAB-0102", "NYY 3x400 mm²", "Multi Core", 69, 0, 12.765],
    ["KAB-0103", "NYY 4x1.5 mm²", "Multi Core", 13.8, 0, 0.259],
    ["KAB-0104", "NYY 4x2.5 mm²", "Multi Core", 15, 0, 0.324],
    ["KAB-0105", "NYY 4x4 mm²", "Multi Core", 17.3, 0, 0.453],
    ["KAB-0106", "NYY 4x6 mm²", "Multi Core", 18.7, 0, 0.563],
    ["KAB-0107", "NYY 4x10 mm²", "Multi Core", 21.5, 0, 0.794],
    ["KAB-0108", "NYY 4x16 mm²", "Multi Core", 23.5, 0, 1.083],
    ["KAB-0109", "NYY 4x25 mm²", "Multi Core", 27.5, 0, 1.558],
    ["KAB-0110", "NYY 4x35 mm²", "Multi Core", 30, 0, 2.018],
    ["KAB-0111", "NYY 4x50 mm²", "Multi Core", 35.5, 0, 2.466],
    ["KAB-0112", "NYY 4x70 mm²", "Multi Core", 39, 0, 3.334],
    ["KAB-0113", "NYY 4x95 mm²", "Multi Core", 44.5, 0, 4.491],
    ["KAB-0114", "NYY 4x120 mm²", "Multi Core", 48.5, 0, 5.504],
    ["KAB-0115", "NYY 4x150 mm²", "Multi Core", 54.5, 0, 6.787],
    ["KAB-0116", "NYY 4x185 mm²", "Multi Core", 59, 0, 8.392],
    ["KAB-0117", "NYY 4x240 mm²", "Multi Core", 66, 0, 10.818],
    ["KAB-0118", "NYY 4x300 mm²", "Multi Core", 72.5, 0, 13.326],
    ["KAB-0119", "NYY 4x400 mm²", "Multi Core", 82.5, 0, 16.969],
    ["KAB-0120", "NYY 5x1.5 mm²", "Multi Core", 14.8, 0, 0.302],
    ["KAB-0121", "NYY 5x2.5 mm²", "Multi Core", 16, 0, 0.382],
    ["KAB-0122", "NYY 5x4 mm²", "Multi Core", 18.7, 0, 0.541],
    ["KAB-0123", "NYY 5x6 mm²", "Multi Core", 20.5, 0, 0.677],
    ["KAB-0124", "NYY 5x10 mm²", "Multi Core", 23, 0, 0.954],
    ["KAB-0125", "NYY 5x16 mm²", "Multi Core", 26, 0, 1.309],
    ["KAB-0126", "NYY 5x25 mm²", "Multi Core", 30, 0, 1.895],
    ["KAB-0127", "NYY 5x35 mm²", "Multi Core", 33, 0, 2.478],
    ["KAB-0128", "NYY 5x50 mm²", "Multi Core", 38, 0, 3.161],
    ["KAB-0129", "NYFGbY 2x1.5 mm²", "Multi Core", 17.9, 0, 0.611],
    ["KAB-0130", "NYFGbY 2x2.5 mm²", "Multi Core", 18, 0, 0.63],
    ["KAB-0131", "NYFGbY 2x4 mm²", "Multi Core", 17.8, 0, 0.648],
    ["KAB-0132", "NYFGbY 2x6 mm²", "Multi Core", 18.1, 0, 0.687],
    ["KAB-0133", "NYFGbY 2x10 mm²", "Multi Core", 20.5, 0, 0.881],
    ["KAB-0134", "NYFGbY 2x16 mm²", "Multi Core", 22.5, 0, 1.085],
    ["KAB-0135", "NYFGbY 2x25 mm²", "Multi Core", 25.5, 0, 1.428],
    ["KAB-0136", "NYFGbY 2x35 mm²", "Multi Core", 27.5, 0, 1.753],
    ["KAB-0137", "NYFGbY 2x50 mm²", "Multi Core", 31, 0, 2.12],
    ["KAB-0138", "NYFGbY 2x70 mm²", "Multi Core", 35, 0, 2.716],
    ["KAB-0139", "NYFGbY 2x95 mm²", "Multi Core", 39.5, 0, 3.528],
    ["KAB-0140", "NYFGbY 2x120 mm²", "Multi Core", 43, 0, 4.177],
    ["KAB-0141", "NYFGbY 2x150 mm²", "Multi Core", 47, 0, 5.012],
    ["KAB-0142", "NYFGbY 2x185 mm²", "Multi Core", 52, 0, 6.116],
    ["KAB-0143", "NYFGbY 2x240 mm²", "Multi Core", 58, 0, 7.695],
    ["KAB-0144", "NYFGbY 2x300 mm²", "Multi Core", 64, 0, 9.384],
    ["KAB-0145", "NYFGbY 3x1.5 mm²", "Multi Core", 17.8, 0, 0.62],
    ["KAB-0146", "NYFGbY 3x2.5 mm²", "Multi Core", 18, 0, 0.648],
    ["KAB-0147", "NYFGbY 3x4 mm²", "Multi Core", 18, 0, 0.681],
    ["KAB-0148", "NYFGbY 3x6 mm²", "Multi Core", 19, 0, 0.789],
    ["KAB-0149", "NYFGbY 3x10 mm²", "Multi Core", 21.5, 0, 0.997],
    ["KAB-0150", "NYFGbY 3x16 mm²", "Multi Core", 23.5, 0, 1.28],
    ["KAB-0151", "NYFGbY 3x25 mm²", "Multi Core", 27, 0, 1.709],
    ["KAB-0152", "NYFGbY 3x35 mm²", "Multi Core", 29.5, 0, 2.123],
    ["KAB-0153", "NYFGbY 3x50 mm²", "Multi Core", 32, 0, 2.444],
    ["KAB-0154", "NYFGbY 3x70 mm²", "Multi Core", 36, 0, 3.217],
    ["KAB-0155", "NYFGbY 3x95 mm²", "Multi Core", 40.5, 0, 4.171],
    ["KAB-0156", "NYFGbY 3x120 mm²", "Multi Core", 43.5, 0, 4.957],
    ["KAB-0157", "NYFGbY 3x150 mm²", "Multi Core", 48, 0, 6.011],
    ["KAB-0158", "NYFGbY 3x185 mm²", "Multi Core", 52.5, 0, 7.319],
    ["KAB-0159", "NYFGbY 3x240 mm²", "Multi Core", 59, 0, 9.324],
    ["KAB-0160", "NYFGbY 3x300 mm²", "Multi Core", 64.5, 0, 11.344],
    ["KAB-0161", "NYFGbY 4x1.5 mm²", "Multi Core", 17.9, 0, 0.63],
    ["KAB-0162", "NYFGbY 4x2.5 mm²", "Multi Core", 18, 0, 0.662],
    ["KAB-0163", "NYFGbY 4x4 mm²", "Multi Core", 19, 0, 0.773],
    ["KAB-0164", "NYFGbY 4x6 mm²", "Multi Core", 20.5, 0, 0.912],
    ["KAB-0165", "NYFGbY 4x10 mm²", "Multi Core", 23, 0, 1.174],
    ["KAB-0166", "NYFGbY 4x16 mm²", "Multi Core", 25.5, 0, 1.52],
    ["KAB-0167", "NYFGbY 4x25 mm²", "Multi Core", 29, 0, 2.053],
    ["KAB-0168", "NYFGbY 4x35 mm²", "Multi Core", 32, 0, 2.584],
    ["KAB-0169", "NYFGbY 4x50 mm²", "Multi Core", 37.5, 0, 3.173],
    ["KAB-0170", "NYFGbY 4x70 mm²", "Multi Core", 40.5, 0, 4.087],
    ["KAB-0171", "NYFGbY 4x95 mm²", "Multi Core", 46.5, 0, 5.359],
    ["KAB-0172", "NYFGbY 4x120 mm²", "Multi Core", 50.5, 0, 6.459],
    ["KAB-0173", "NYFGbY 4x150 mm²", "Multi Core", 56.5, 0, 7.868],
    ["KAB-0174", "NYFGbY 4x185 mm²", "Multi Core", 61, 0, 9.556],
    ["KAB-0175", "NYFGbY 4x240 mm²", "Multi Core", 68, 0, 12.138],
    ["KAB-0176", "NYFGbY 4x300 mm²", "Multi Core", 74.5, 0, 14.773],
    ["KAB-0177", "NYFGbY 5x1.5 mm²", "Multi Core", 17.8, 0, 0.642],
    ["KAB-0178", "NYFGbY 5x2.5 mm²", "Multi Core", 17.9, 0, 0.682],
    ["KAB-0179", "NYFGbY 5x4 mm²", "Multi Core", 20.5, 0, 0.891],
    ["KAB-0180", "NYFGbY 5x6 mm²", "Multi Core", 22, 0, 1.056],
    ["KAB-0181", "NYFGbY 5x10 mm²", "Multi Core", 24.5, 0, 1.391],
    ["KAB-0182", "NYFGbY 5x16 mm²", "Multi Core", 27.5, 0, 1.778],
    ["KAB-0183", "NYFGbY 5x25 mm²", "Multi Core", 32, 0, 2.463],
    ["KAB-0184", "NYFGbY 5x35 mm²", "Multi Core", 35, 0, 3.106],
    ["KAB-0185", "NYFGbY 5x50 mm²", "Multi Core", 40, 0, 3.904]
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
    ["TRY-0001", "Tray 100x100 mm", 100, 100, 20],
    ["TRY-0002", "Tray 200x100 mm", 200, 100, 40],
    ["TRY-0003", "Tray 300x100 mm", 300, 100, 60],
    ["TRY-0004", "Tray 400x100 mm", 400, 100, 80],
    ["TRY-0005", "Tray 500x100 mm", 500, 100, 100],
    ["TRY-0006", "Tray 600x100 mm", 600, 100, 120],
    ["TRY-0007", "Tray 800x100 mm", 800, 100, 160],
    ["TRY-0008", "Tray 1000x100 mm", 1000, 100, 200],
    ["TRY-0009", "Tray 1200x100 mm", 1200, 100, 240]
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
/**
 * ====================================================================
 * INISIALISASI SHEET "Users" (GERBANG LOGIN)
 * ====================================================================
 * Jalankan initUsers() SEKALI untuk membuat sheet Users + akun awal.
 *
 * ⚠ PENTING — KEAMANAN:
 * Daftar SEED_USERS di bawah sengaja DIKOSONGKAN di repo GitHub.
 * Password TIDAK BOLEH ikut ter-commit: sekali masuk riwayat git, ia
 * tersimpan selamanya walau baris-nya dihapus belakangan.
 *
 * Cara pakai:
 *   1. Buka editor Apps Script Anda (script.google.com) -> file Setup.gs
 *   2. Isi SEED_USERS dengan akun yang diinginkan, contoh:
 *        var SEED_USERS = [
 *          { username: "admin", password: "....", role: "admin" },
 *          { username: "user1", password: "....", role: "user"  }
 *        ];
 *   3. Jalankan initUsers() sekali -> sheet Users terisi (HANYA hash,
 *      password asli tidak pernah disimpan di spreadsheet).
 *   4. KOSONGKAN lagi SEED_USERS di editor supaya password tidak
 *      tertinggal di sana. Selanjutnya tambah user lewat dashboard admin.
 *
 * Menjalankan ulang initUsers() TIDAK menghapus user yang sudah ada —
 * hanya menambah yang belum terdaftar.
 */
var SEED_USERS = [
  // { username: "admin", password: "ISI_DI_EDITOR_ANDA", role: "admin" }
];

function initUsers() {
  var sheet = ensureUsersSheet_();

  if (!SEED_USERS || SEED_USERS.length === 0) {
    throw new Error(
      "SEED_USERS masih kosong. Isi dulu daftar akun di bagian atas fungsi " +
      "ini (di editor Apps Script Anda, JANGAN di repo GitHub), lalu " +
      "jalankan initUsers() lagi.");
  }

  var existing = {};
  var values = sheet.getDataRange().getValues();
  for (var i = 1; i < values.length; i++) {
    if (values[i][0]) existing[values[i][0].toString().toLowerCase()] = true;
  }

  var added = 0;
  for (var j = 0; j < SEED_USERS.length; j++) {
    var u = SEED_USERS[j];
    if (!u || !u.username || !u.password) continue;
    var uname = u.username.toString().trim();
    if (existing[uname.toLowerCase()]) continue;

    var salt = Utilities.getUuid().replace(/-/g, "");
    sheet.appendRow([
      uname,
      hashPassword_(u.password, salt),
      salt,
      (u.role === "admin" ? "admin" : "user"),
      true,
      new Date(),
      ""
    ]);
    added++;
  }

  SpreadsheetApp.flush();
  Logger.log("initUsers selesai. Akun baru ditambahkan: " + added);
  return added;
}

/**
 * Membuat sheet Users beserta headernya bila belum ada (tanpa menghapus data).
 */
function ensureUsersSheet_() {
  var ss = SpreadsheetApp.getActiveSpreadsheet();
  var sheet = ss.getSheetByName("Users");
  if (!sheet) {
    sheet = ss.insertSheet("Users");
    var header = ["Username", "Password_Hash", "Salt", "Role", "Aktif",
                  "Dibuat_Pada", "Terakhir_Login"];
    sheet.appendRow(header);
    sheet.getRange(1, 1, 1, header.length)
      .setFontWeight("bold").setBackground("#FF9800").setFontColor("#FFFFFF");
    sheet.setFrozenRows(1);
  }
  return sheet;
}
