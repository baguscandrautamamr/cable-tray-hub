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
| `sw.js` | Service worker (cache `cable-tray-hub-v6`; index network-first; API Google TIDAK diintersep) |
| `Code.gs` | REST API Apps Script: `?action=getInitialData`, `?action=getSimulation&id=`, POST `saveSimulation` & `deleteSimulation`, sheet `Revit_Sync` untuk Push |
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

**Perbaikan ronde uji ke-2 (17 Jul, dari temuan uji Revit + web):**
1. **Anti-clash arm tray**: posisi conduit dijepit terhadap dimensi tray NYATA
   di Revit (parameter Width/Height elemen terpilih) dengan jarak aman 10 mm
   dari dinding + OD conduit efektif minimal 21 mm (kabel kecil tetap digambar
   Revit sebagai conduit ukuran terkecil). Web juga memberi margin 10 mm dari
   dinding samping (posisi default, batas drag, dan hasil simpan).
2. **Riwayat lengkap & bisa diedit ulang**: kartu riwayat kini menampilkan
   jumlah kabel + tipe per jenis; tombol Edit/Buka merestore SEMUA data dari
   `detail` tersimpan (kabel+qty per jalur, dimensi tray, metode, spare,
   sampai posisi tiap kabel di kanvas penampang).
3. **Bend Radius elbow conduit berpatokan ke FITTING tray terpilih**: baca
   parameter "Bend Radius" fitting elbow terdekat dari titik belok (konvensi
   family tray Revit = radius SISI DALAM belokan → radius sumbu = BendRadius +
   lebar/2). Tiap conduit dibuat konsentris & DIJEPIT agar busurnya tetap di
   dalam annulus elbow tray (min sisi dalam + OD/2, max sisi luar − OD/2).
   Estimasi geometris hanya fallback bila fitting tak punya parameter radius.
4. **Trefoil tidak terbalik lagi**: frame penampang kini diikat ke segmen
   HORIZONTAL pertama rantai (bukan segmen pertama sembarang) lalu parallel
   transport maju+mundur — kalau rantai mulai dari riser vertikal, arah "atas"
   tak lagi ambigu sehingga kabel puncak trefoil selalu di atas. Kanvas web:
   kabel puncak trefoil kini tepat di tengah dua kabel dasar.

**Perbaikan ronde uji ke-3 (17 Jul, temuan lanjutan di Revit):**
1. **Interior tray diukur dari geometri solid** (bukan parameter): probe garis
   di 3 stasiun sepanjang sumbu → puncak plat dasar (kabel duduk DI ATAS plat,
   bukan menembus dasar setebal 25.4 mm) + sisi DALAM rail kiri/kanan.
   Koordinat y website kini dipetakan dari puncak plat dasar interior.
2. **Family elbow conduit ber-lookup table terdeteksi otomatis**: probe elbow
   percobaan di SubTransaction (langsung rollback). Kalau Bend Radius terkunci
   formula (mis. `ACT_Elbow_RMC`: `size_lookup(..., "BRad", ...)`), belokan
   berradius digambar sebagai **rangkaian chord per ≤15° mengikuti busur
   konsentris elbow tray** — tiap kabel tetap dapat radius sendiri dan tetap
   di dalam annulus elbow. Kalau radius bisa di-set, tetap elbow tunggal.

**Ronde 4 (17 Jul): tombol Hapus riwayat kini menghapus PERMANEN di database**
(`deleteSimulation` di Code.gs + konfirmasi ganda di web; entri lokal-saja
tetap bisa dihapus dari daftar). ⚠ WAJIB: salin Code.gs terbaru ke editor
Apps Script lalu Deploy → Manage deployments → Edit → **New version** —
sebelum itu tombol Hapus akan menampilkan error "Unknown action".

**Ronde 5 (17 Jul): rollback mode chord + fix kabel melayang**
1. Mode chord (ronde 3) DICABUT atas permintaan — di lapangan menghasilkan
   ribuan segmen kecil + error "insufficient space to create fittings"
   (1470 conduit utk 1 jalur). Belokan kembali seperti ronde 2: satu elbow
   per belokan, Bend Radius konsentris di-set bila family mengizinkan
   (family ber-lookup table = pakai radius default family, terima saja).
2. Kabel melayang ±70 mm di atas plat: pengukur interior teracuni segmen
   riser (dinding terbaca sebagai "lantai" → semua kabel terangkat ke klem
   atas). Fix: segmen dengan |arah.Z| > 0.7 dilewati, dan hanya struktur yang
   SELURUHNYA di bawah sumbu dihitung lantai (yang melintasi sumbu diabaikan);
   rail samping juga hanya yang seluruhnya di satu sisi.

**Ronde 6 (17 Jul): jarak aman conduit bisa diatur dari dialog Pull**
- Temuan uji: conduit masih menabrak penampang tray (baris kabel menyentuh
  plat dasar). Fix: dialog Pull kini punya dua input "Jarak aman conduit (mm)"
  — **ke dasar tray** dan **ke arm samping** (default 10/10, diingat di
  config `%APPDATA%\CableTrayHub\config.json`). Jarak dasar mengangkat seluruh
  susunan kabel dari puncak plat; jarak samping menggantikan konstanta 10 mm
  yang dulu hard-coded. Clamp posisi tetap memakai OD conduit efektif (min
  21 mm) sehingga badan conduit — bukan hanya sumbunya — yang diberi jarak.

**Ronde 7 (18 Jul): PWA installable + ikon add-in**
- Manifest kini memakai ikon PNG asli (`/icons/icon-192/512.png` + varian
  maskable) — sebelumnya SVG data-URI ber-emoji yang DITOLAK Chrome Android
  sebagai syarat install. Tambah apple-touch-icon & favicon. sw cache v7
  (ikon ikut di-precache).
- Mobile: `html,body{overflow-x:hidden}` + `img,canvas,table{max-width:100%}`
  — halaman tidak bisa scroll ke kanan lagi.
- Add-in Revit: tombol Pull/Push kini ber-ikon (PNG 32/16 tertanam sebagai
  EmbeddedResource di DLL, dimuat via BitmapImage; csproj UseWPF=true).

**Ronde 8 (18 Jul): jarak kabel ke dinding di web bisa diatur (default 0)**
- Temuan: kabel di kanvas penampang tidak bisa menempel pinggir tray — margin
  10 mm dari ronde 2 ternyata hard-coded di web. Fix: konstanta diganti input
  "Jarak Kabel ke Dinding (mm)" di form simulasi (samping Spare Space Factor),
  default **0 = kabel boleh menempel rail**. Berlaku untuk posisi default,
  batas drag, dan koordinat mm yang disimpan; nilainya ikut tersimpan di
  `detail.sideMargin` dan direstore saat Edit/Buka riwayat (riwayat lama tanpa
  field ini dianggap 0). sw cache naik ke v8.
- Catatan: jarak aman di REVIT tetap dari dialog Pull (ronde 6). Kalau mau
  conduit di Revit juga menempel arm, set "jarak ke arm samping" = 0 di dialog.

**Ronde 9 (22 Jul): conduit tidak lagi saling tumpuk (rapat pakai OD nyata)**
- Temuan: conduit SALING TUMPUK di penampang Revit walau susunan di kanvas web
  sudah benar. Sebabnya BUKAN posisinya — posisi web sudah tak overlap MEMAKAI
  diameter kabel. Tapi Revit menggambar conduit sebesar ukuran di **Conduit
  Sizes** family; kalau diameter kabel tak ada di tabel, ukuran ter-snap ke
  yang terdekat/ default (lebih besar) → tabung conduit lebih lebar dari jarak
  antar-titiknya → tumpuk. (`PullCommand.cs:466` set nominal = diameter kabel.)
- Fix (pilihan user: *rapatkan ulang pakai OD asli*): plugin kini **mengukur OD
  LUAR nyata** tiap ukuran kabel lewat probe conduit di `SubTransaction` (baca
  `RBS_CONDUIT_OUTER_DIAM_PARAM`, langsung rollback). Pola & BARIS dari kanvas
  web DIPERTAHANKAN (dikelompokkan dari `pos.Y`, urut kiri→kanan dari `pos.X`),
  tapi jarak antar-conduit dihitung dari OD nyata → tabung bersentuhan tanpa
  tumpuk. Baris atas bersarang di lembah (offset rMax, naik rMax·√3).
- Input worksheet (posisi tiap kabel) & input conduit TIDAK dihapus — posisi web
  tetap jadi acuan pola/urutan. Seluruh susunan duduk di atas `floorV +
  bottomClr` → ubah "jarak ke dasar tray" di dialog Pull mengangkat SEMUA conduit
  seragam. Clamp atas dihapus: kalau melebihi kapasitas, baris atas naik keluar
  tray (sinyal penuh) alih-alih dipaksa tumpuk.

**Ronde 9b (22 Jul, digabung dari branch terpisah): Tipe conduit + Workset di
dialog Pull**
- Dialog Pull dapat dua dropdown baru: **Tipe conduit** (semua ConduitType di
  proyek, format "Family: Type") dan **Workset** (hanya tampil bila model
  workshared; conduit/elbow baru dibuat masuk ke workset itu via
  `SetActiveWorksetId` sebelum transaction, dikembalikan ke workset semula
  sesudahnya). Pilihan diingat di config (`LastConduitType`, `LastWorkset`);
  kalau belum pernah pilih → fallback otomatis ke tipe yang punya aturan Elbow.
- Percobaan re-implementasi penataan penampang langsung dari nama metode
  (`ArrangeCrossSection`, baca `sim.Detail.Metode`) di branch yang sama TIDAK
  dipakai — diganti tetap pakai pendekatan Ronde 9/10 (replay posisi dari
  kanvas web) karena itu sudah teruji dan otomatis ikut metode apa pun +
  hasil drag manual user, tanpa perlu logika Trefoil terpisah di C#.

**Ronde 10 (1 Agu): kanvas web gepeng/menumpuk + conduit kecil melayang**
- Laporan: (1) kanvas penampang di web terlihat menumpuk/lonjong setiap
  tambah kabel; (2) di Revit conduit kabel terkecil melayang, tidak sejajar
  dasar tray dengan yang lain.
- Fix #1 (`index.html`): `<canvas id="trayVisualizer">` tak punya atribut
  `width`/`height`, jadi buffer gambarnya default ke ukuran bawaan browser
  300×150px sementara tampilannya di-CSS ke `w-full × 220px` — buffer kecil
  itu diregangkan browser TIDAK PROPORSIONAL ke ukuran tampil sebenarnya,
  bikin lingkaran kabel lonjong & terlihat menumpuk. Ditambahkan
  `syncCanvasResolution()` yang menyamakan resolusi buffer dengan ukuran
  tampil sebelum menghitung posisi & menggambar.
- Fix #2 (`PullCommand.cs`): deteksi baris (row) conduit per jalur dulu
  memakai SATU ambang batas global (radius kabel TERBESAR se-jalur) untuk
  memisahkan baris dasar vs baris apex trefoil. Kalau jalur berisi campuran
  kabel besar & kecil, ambang yang kegedean bikin baris apex kabel kecil ikut
  "kesedot" ke baris kabel besar (atau sebaliknya) → conduit kecil melayang,
  tidak sejajar. Sekarang baris dihitung PER KELOMPOK kabel (radius kabel itu
  sendiri sebagai ambang), baris dasar tiap kelompok tetap dianggap "duduk di
  lantai tray" yang sama untuk semua kelompok.

**Ronde 11 (1 Agu): susunan Revit ≠ web (tercampur pola Flat Spaced)**
- Laporan: web di-set **Trefoil**, tapi hasil Pull di Revit conduit
  direnggangkan seragam seperti Flat Spaced (De) dan 3 conduit paling kanan
  meluber KELUAR tray.
- Akar masalah (`PullCommand.cs`): penataan lateral memakai
  `stepFt = 2 * rMaxFt` — jarak antar-kolom SERAGAM sebesar OD conduit
  **TERBESAR di jalur itu**, dipakai untuk SEMUA conduit tanpa peduli
  ukurannya sendiri (`slotLat[s] = availLeft + rMax + off + i*stepFt`).
  Jadi conduit kecil ikut diberi jarak sebesar conduit terbesar → persis
  penampakan "Flat Spaced", dan total lebarnya membengkak jauh sehingga
  barisan tumpah keluar tray. Tiap baris juga di-repack ulang dari tepi kiri
  secara terpisah, jadi apex trefoil tak lagi sejajar di atas pasangan
  dasarnya.
- Fix: pola kanvas web di-REPLAY, bukan dihitung ulang per metode.
  1. Per KELOMPOK kabel (satu jenis/diameter — satu kelompok trefoil selalu
     sejenis), x & y diskala rasio OD-nyata/diameter kabel kelompok itu.
     Karena serumpun memakai rasio SAMA, geometri internal tetap presisi:
     apex trefoil tepat di tengah dua kabel dasar, kabel dasar bersentuhan
     (jarak pusat = OD).
  2. Kelompok berikutnya di-anchor menyambung dari ujung kanan kelompok
     sebelumnya (jarak antar-kelompok dari web dipertahankan) supaya urutan
     kiri→kanan tidak tertukar.
  3. Bentrokan sisa di seam antar-kelompok diselesaikan dengan menggeser
     SATU KELOMPOK UTUH (rigid) ke kanan sejauh yang perlu — besarnya eksak
     dari geometri lingkaran `dx = √((ri+rj)² − dv²)` — jadi bentuk trefoil
     tak ikut berubah.
- Diverifikasi numerik memakai posisi asli dari kanvas web (Trefoil; NYY
  4x120 ×9 + 4x35 ×6 + 4x300 ×3) dengan OD conduit di-snap ke trade size:
  lebar terpakai **869,6 mm → 635,6 mm** (tray 600), nol pasangan tumpuk,
  dan semua apex trefoil tepat di titik tengah pasangan dasarnya.

**Belum diuji (kerjaan berikutnya):**
-2. Uji ronde 10 di Revit: Pull jalur yang berisi campuran kabel besar+kecil
   (mis. trefoil) → cek conduit kabel kecil kini sejajar dasar, tidak melayang
   lagi dibanding conduit lain di baris yang sama.
-1. Uji ronde 9 di Revit: Pull jalur yang tadinya tumpuk → cek conduit kini
   bersentuhan tanpa tumpuk (pola tetap seperti kanvas web); ubah "jarak ke
   dasar tray" 10→30 → cek SEMUA conduit ikut naik. Kalau family conduit tak
   punya ukuran yang cocok, OD nyata > diameter kabel → jarak otomatis melebar.
0. Uji ronde 9b: dialog Pull tampil dropdown Tipe conduit + Workset → pilih →
   conduit masuk ke tipe & workset itu; nilai diingat di pull berikutnya.
0. Uji ronde 6: install build terbaru → dialog Pull menampilkan 2 input jarak
   → coba mis. dasar 20 / samping 15 → cek di penampang: kabel terangkat dari
   plat & menjauh dari arm, nilai diingat di pull berikutnya.
1. Install build terbaru di mesin Revit (link unduh di atas) → uji Pull ulang:
   cek conduit tidak menabrak arm tray, Bend Radius conduit mengikuti Bend
   Radius elbow tray yang diselect (coba radius 100/200/300), trefoil apex di
   atas, turunan vertikal nyambung.
2. Uji web: simpan simulasi baru → buka tab Riwayat → kartu menampilkan daftar
   kabel → Edit/Buka mengembalikan kabel+posisi persis.
3. Jalur yang pernah di-pull sebelum `7c5b241` mengingat seleksi lama TANPA
   fitting — select ulang jalurnya saat diminta (atau hapus conduit ber-tag CTH).
4. Kalau posisi kabel ter-mirror kiri-kanan: negasikan `lat` di PullCommand.
5. Kalau Bend Radius conduit tidak mengikuti fitting: cek nama parameter radius
   family elbow TRAY (yang dibaca: "Bend Radius"/"Bending Radius"/"BendRadius",
   instance lalu type) — catat nama family & parameternya.

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
