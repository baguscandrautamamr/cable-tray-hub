# Cable Tray Hub — Plugin Revit 2025

Add-in Revit yang menarik data simulasi kabel dari website Cable Tray Hub
(database Google Sheets) lalu menggambar **conduit** paralel di sepanjang
cable tray yang Anda pilih — dari Panel A ke Panel B, lengkap dengan elbow
di belokan.

## Alur pemakaian

1. Di **website**: buat simulasi (pilih tray, susun bundle kabel) → klik
   *Simpan Laporan* → catat **ID simulasi** (misal `SIM-20260717-0002`).
2. Di **Revit**: ribbon **Cable Tray Hub → Import Simulasi**.
3. Isi URL API (sekali saja, akan diingat) + ID simulasi → **Ambil Data** —
   daftar kabel muncul dari database online.
4. Klik **Lanjut: Pilih Cable Tray** → select segmen-segmen tray dari
   Panel A ke Panel B → **Finish**.
5. Plugin menggambar 1 conduit per jalur kabel, paralel mengikuti tray,
   diameter sesuai diameter luar kabel, dengan nama kabel + ID simulasi
   tercatat di parameter *Comments* tiap conduit.

## Build (butuh .NET 8 SDK, TIDAK butuh Revit terinstall)

```powershell
cd revit-plugin/CableTrayHub.Revit
dotnet build -c Release
```

Hasil: `bin/Release/CableTrayHub.Revit.dll`
(Revit API direferensikan lewat paket NuGet `Nice3point.Revit.Api.* 2025`,
jadi bisa dikompilasi di komputer mana pun.)

## Install di komputer yang ada Revit 2025

Salin ke folder addins Revit:

```
%APPDATA%\Autodesk\Revit\Addins\2025\
├── CableTrayHub.addin
└── CableTrayHub\
    └── CableTrayHub.Revit.dll
```

Contoh perintah PowerShell:

```powershell
$dst = "$env:APPDATA\Autodesk\Revit\Addins\2025"
New-Item -ItemType Directory -Force "$dst\CableTrayHub"
Copy-Item CableTrayHub.addin $dst
Copy-Item CableTrayHub.Revit\bin\Release\CableTrayHub.Revit.dll "$dst\CableTrayHub"
```

Buka Revit → saat pertama kali muncul dialog keamanan add-in → pilih
**Always Load**. Tab **Cable Tray Hub** akan muncul di ribbon.

## Catatan teknis

- Project Revit harus punya minimal satu **Conduit Type** (ada di template
  Electrical bawaan). Jika tidak ada, plugin menampilkan pesan.
- Diameter conduit di-set ke diameter luar (De) kabel. Jika ukuran itu tidak
  terdaftar di *Electrical Settings → Conduit Sizes*, Revit memakai ukuran
  default type — plugin melaporkan jumlahnya di ringkasan akhir.
- Jika segmen tray yang dipilih tidak menerus (ada cabang/terputus jauh),
  conduit tetap digambar per segmen tapi tanpa elbow — dilaporkan di
  ringkasan.
- Konfigurasi (URL API, ID terakhir) disimpan di
  `%APPDATA%\CableTrayHub\config.json`.
