# Cable Tray Project Hub — Catatan Proyek & Serah-Terima

> Dokumen ini untuk melanjutkan pekerjaan di komputer lain (mis. laptop kantor).
> Terakhir diperbarui: **17 Juli 2026**.

## Ringkasan

Web kalkulator kapasitas cable tray (katalog 185 kabel KMI) yang tersambung ke
database Google Sheets dan bisa sinkron dua arah dengan Revit 2025 lewat plugin.

```
index.html (vanilla JS + Tailwind CDN, PWA)
      ⇅  REST API
Google Apps Script (Code.gs)  ⇄  Google Sheets
      ⇅  Pull / Push
Plugin Revit 2025 (.NET 8, C#) — revit-plugin/
```

## Tautan Penting

| Apa | URL |
|---|---|
| Repo GitHub | https://github.com/baguscandrautamamr/cable-tray-hub |
| Situs live (Vercel) | https://cable-tray-hub.vercel.app |
| Unduh plugin Revit (selalu build terbaru) | https://github.com/baguscandrautamamr/cable-tray-hub/releases/download/latest/CableTrayHub-Revit2025.zip |
| API Apps Script (sudah terisi di index.html) | `https://script.google.com/macros/s/AKfycbxh2miRJywQ354kehZ9i6nKgBBEe_Xn8q9fBBN67tomXRc1Zm2YFq8JyfoYt7QpmxjtrA/exec` |

Catatan: `ebacable.vercel.app` adalah project Vercel LAMA yang tidak tersambung
repo — abaikan. Project yang benar: **cable-tray-hub** (akun Vercel Hobby,
auto-deploy setiap push ke `main`).

## Struktur Repo

| File/Folder | Isi |
|---|---|
| `index.html` | Seluruh aplikasi web (multi-jalur per simulasi: `dbRoutes` panelFrom/panelTo/cables; kanvas penampang drag & drop menyimpan posisi mm tiap kabel) |
| `sw.js` | Service worker (cache `cable-tray-hub-v4`; index network-first; API Google TIDAK diintersep) |
| `Code.gs` | REST API Apps Script: `?action=getInitialData`, `?action=getSimulation&id=`, POST `saveSimulation`, sheet `Revit_Sync` untuk Push |
| `Setup.gs` | `initSetup()` — mengisi katalog 185 kabel (identik dengan website) ke sheet `Katalog_Kabel`, `Tray_Templates`, `Simulasi_History` |
| `revit-plugin/` | Plugin Revit 2025: `PullCommand.cs` (gambar conduit), `PushCommand.cs` (kirim data conduit ke sheet), `SyncStorage.cs` (Extensible Storage: mapping jalur→tray), `SimulationDialog.cs`, `ApiClient.cs` |
| `.github/workflows/build-revit-plugin.yml` | CI: build .NET 8 → artifact + **Release tag `latest`** otomatis tiap push yang menyentuh `revit-plugin/` |

## Status per 17 Juli 2026

**Sudah beres & teruji:**
- Database hidup: `initSetup` sudah dijalankan, save/pull/push teruji end-to-end.
- Vercel tersambung repo, situs live identik dengan `main` (sw v4, API_URL terisi).
- Release `latest` otomatis terbit dari CI dengan link unduh publik.
- Uji Revit nyata pertama menemukan 3 bug Pull → **diperbaiki di `7c5b241`**:
  1. Fitting/elbow tray kini bisa ikut di-select (dulu filter menolak) dan
     dipakai sebagai jembatan perantaian segmen (elbow radius besar / riser
     vertikal > 600 mm tetap terantai).
  2. Elbow conduit kini terbentuk: plugin memilih ConduitType yang punya aturan
     Elbow di Routing Preferences (dulu ambil tipe pertama sembarang — kalau
     dapat "without fittings", `NewElbowFitting` gagal diam-diam).
  3. Turunan vertikal: orientasi penampang dihitung parallel transport (frame
     ikut berputar di belokan) — susunan kabel tidak melompat lagi.
- Fitur `9b7a02c`: **elbow conduit konsentris mengikuti busur elbow tray**.
  Radius busur tray dihitung geometris (R = T/tan(θ/2)); Bend Radius tiap elbow
  conduit = R ± offset kabel (sisi dalam tajam, sisi luar landai).

**Belum diuji (kerjaan berikutnya):**
1. Install build terbaru di mesin Revit (link unduh di atas) → uji Pull dengan
   **elbow tray ikut diseleksi**. Cek: elbow konsentris, turunan vertikal
   nyambung, posisi penampang tidak ter-mirror.
2. Jalur yang pernah di-pull sebelum `7c5b241` mengingat seleksi lama TANPA
   fitting — select ulang jalurnya saat diminta (atau hapus conduit ber-tag CTH).
3. Kalau posisi kabel ter-mirror kiri-kanan: negasikan `lat` di PullCommand.
4. Kalau Bend Radius tidak berubah: kemungkinan family elbow conduit mengunci
   parameternya / namanya bukan "Bend Radius" — catat nama family-nya.

## Cara Kerja Plugin (untuk pengujian)

- Install: ekstrak zip → salin `CableTrayHub.addin` + folder `CableTrayHub` ke
  `%APPDATA%\Autodesk\Revit\Addins\2025\` → buka Revit → tab **Add-Ins**.
- **Pull**: masukkan ID simulasi → per jalur, pilih segmen tray **dan
  fitting-nya** → conduit digambar mengikuti posisi penampang dari website.
  Pilihan tray diingat dalam file Revit (Extensible Storage), pull berikutnya
  tidak perlu select lagi. Conduit lama ber-tag jalur itu dihapus otomatis.
- **Push**: mengirim jumlah & panjang conduit per jalur ke sheet `Revit_Sync`.
- Penanda elemen: parameter Comments = `CTH|<jalur>|<nama kabel>`.
- Orientasi penampang: dilihat searah Panel Asal → Panel Tujuan; x dari dinding
  kiri tray, y dari dasar tray.

## Gotcha / Pelajaran (jangan diulang)

- **Apps Script + curl**: JANGAN `-X POST` (redirect 302 → error 411). Cukup
  `--data-binary`, biarkan curl konversi ke GET saat redirect.
- **PowerShell 5.1**: kutip ganda dalam pesan commit merusak argumen git —
  pakai `git commit -F file` / heredoc di Git Bash.
- **TaskDialog ambigu** antara `Autodesk.Revit.UI` dan `System.Windows.Forms` —
  harus di-alias (sudah ada di kode).
- **Conduit Revit tidak bisa melengkung** — lengkungan selalu dari fitting
  elbow; radiusnya via parameter instance "Bend Radius".
- **Outage GitHub (16–17 Jul 2026)**: error 500 "Unicorn" membuat langkah
  release CI gagal & webhook Vercel tertahan. Solusi: cek
  https://www.githubstatus.com lalu re-run workflow — bukan salah konfigurasi.
  Workflow bisa dipicu via API:
  `POST /repos/baguscandrautamamr/cable-tray-hub/actions/workflows/build-revit-plugin.yml/dispatches`
  body `{"ref":"main"}` (butuh token GitHub).
- **Vercel Hobby**: build antre satu per satu — "Upgrade to Pro" tidak perlu.
- **sw.js**: kalau deploy tak terlihat di browser, naikkan versi cache
  (`cable-tray-hub-vN`) — index.html sudah network-first sejak v4.

## Setup di Laptop Kantor

```bash
git clone https://github.com/baguscandrautamamr/cable-tray-hub.git
```

- Edit web: cukup file `index.html` / `sw.js`, push ke `main` → Vercel deploy
  otomatis (±1 menit).
- Edit plugin: file di `revit-plugin/`, push → CI GitHub Actions build otomatis
  (~2 menit) → unduh dari link Release `latest` di atas. Tidak perlu install
  .NET SDK lokal.
- Edit database/API: `Code.gs` / `Setup.gs` di repo hanyalah salinan; sumber
  aslinya di editor Apps Script akun Google Anda (script.google.com) — setelah
  ubah, **Deploy → Manage deployments → Edit → New version** agar URL /exec
  tetap sama.
