# Cable Tray Hub — Plugin Revit 2025 (Pull/Push Sync)

Add-in Revit yang menyinkronkan data simulasi kabel dari website Cable Tray Hub
ke model Revit, **per jalur Panel Asal → Panel Tujuan**.

## Dua tombol di ribbon "Cable Tray Hub"

### 🔽 Pull dari Website
1. Masukkan URL API + ID simulasi (dari tombol **Connect ke Revit** di website)
   → **Ambil Data** — daftar jalur & kabel tampil.
2. **Jalur baru**: plugin minta Anda select segmen cable tray dari Panel Asal
   ke Panel Tujuan (sekali saja — pilihan diingat di dalam file Revit).
3. **Jalur lama** (pernah di-pull): conduit lama bertanda jalur itu otomatis
   **dihapus dan digambar ulang** sesuai data website terbaru — sinkron tanpa
   duplikat, tanpa select ulang.
4. Conduit digambar paralel mengikuti tray (elbow otomatis di belokan),
   diameter sesuai De kabel, bertanda `CTH|<jalur>|<nama kabel>` di parameter
   *Comments*.

> Ubah kabel di website → Simpan → catat ID baru → Pull lagi di Revit →
> model langsung menyesuaikan.

### 🔼 Push ke Website
Merangkum semua conduit hasil Pull di model (per jalur: jumlah conduit +
total panjang meter) dan mengirimnya ke database website — tercatat di sheet
**Revit_Sync** Google Sheets, jadi engineer tahu jalur mana yang sudah
dieksekusi di model.

## Build (butuh .NET 8 SDK, TIDAK butuh Revit terinstall)

```powershell
cd revit-plugin/CableTrayHub.Revit
dotnet build -c Release
```

Atau otomatis: setiap push, GitHub Actions membuat artifact
**CableTrayHub-Revit2025** di tab Actions.

## Install di komputer yang ada Revit 2025

```
%APPDATA%\Autodesk\Revit\Addins\2025\
├── CableTrayHub.addin
└── CableTrayHub\
    └── CableTrayHub.Revit.dll
```

Buka Revit → dialog keamanan add-in → **Always Load**.

## Catatan teknis

- Pemetaan jalur→tray disimpan di dalam file Revit (Extensible Storage,
  elemen `CableTrayHub_SyncStorage`) — ikut tersimpan bersama model.
- Project harus punya minimal satu **Conduit Type** (template Electrical).
- Diameter conduit di-set ke De kabel; bila ukuran tidak ada di
  *Electrical Settings → Conduit Sizes*, dipakai ukuran default type.
- Jika segmen tray terputus/bercabang, conduit digambar per segmen tanpa
  elbow (dilaporkan di ringkasan).
- Konfigurasi URL API & ID terakhir: `%APPDATA%\CableTrayHub\config.json`.
