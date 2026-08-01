# Cable Tray Project Hub — Online Database Edition

Web kalkulator cable tray yang tersambung ke **Google Sheets sebagai database online**,
bisa diakses dari Vercel maupun plugin Revit lewat REST API.

## Arsitektur

```
┌──────────────────┐     ┌─────────────────────────┐     ┌──────────────────┐
│  Website (Vercel) │ ──► │ Google Apps Script API   │ ──► │  Google Sheets    │
│  index.html       │     │ Code.gs (doGet/doPost)   │     │  3 sheet database │
└──────────────────┘     └─────────────────────────┘     └──────────────────┘
                                    ▲
                                    │  GET ?action=getSimulation&id=SIM-xxx
                          ┌──────────────────┐
                          │  Plugin Revit C#  │
                          └──────────────────┘
```

## File

| File | Fungsi |
|---|---|
| `Code.gs` | Backend Apps Script: web app + REST API JSON + login & manajemen user |
| `Setup.gs` | Pembuat tabel database di Google Sheets (`initSetup`), akun login (`initUsers`) + upgrade tanpa hapus data (`upgradeSheets`) |
| `index.html` | Frontend — bisa di-host di Apps Script ATAU Vercel |
| `revit-plugin/` | Add-in Revit 2025 (C# .NET 8) — lihat [revit-plugin/README.md](revit-plugin/README.md) |

## Gerbang Login

Website dikunci: pengunjung harus login sebelum bisa membuka aplikasi.
Akun disimpan di sheet **`Users`**, password hanya disimpan sebagai
**SHA-256 bergaram** (salt acak per user) — password asli tidak pernah
tersimpan, baik di spreadsheet maupun di repo ini.

Ada dua peran:

| Peran | Bisa apa |
|---|---|
| `user` | Memakai aplikasi (simulasi, riwayat, laporan) |
| `admin` | Semua di atas + kelola katalog kabel/tray + **tambah/hapus/nonaktifkan user** dari menu **Profil → Manajemen User** |

### Menyiapkan akun pertama (sekali saja)

> ⚠ Lakukan langkah ini **sebelum** website versi baru dipakai — selama sheet
> `Users` belum ada, tidak ada seorang pun yang bisa masuk (gerbang sengaja
> menolak bila ragu, bukan membuka).

1. Buka editor Apps Script Anda → salin `Code.gs` dan `Setup.gs` versi terbaru.
2. Di `Setup.gs`, isi `SEED_USERS` **di editor Apps Script** (jangan di repo):
   ```js
   var SEED_USERS = [
     { username: "admin", password: "PASSWORD_ADMIN_ANDA", role: "admin" },
     { username: "user1", password: "PASSWORD_USER_ANDA",  role: "user"  }
   ];
   ```
3. Jalankan fungsi **`initUsers`** sekali → sheet `Users` terbuat & terisi.
4. **Kosongkan lagi `SEED_USERS`** di editor supaya password tidak tertinggal.
5. **Deploy → Manage deployments → Edit → New version** (wajib, supaya endpoint
   login aktif di URL `/exec`).

Selanjutnya semua penambahan user cukup lewat dashboard admin di website.

### Catatan keamanan (baca sebelum mengandalkan ini)

- Password **tidak pernah** ditulis di repo. Kalau suatu saat ada password
  terlanjur ter-commit, **ganti password itu** — riwayat git menyimpannya
  selamanya walau barisnya sudah dihapus.
- Ini aplikasi **statis**, jadi gerbang login menyaring akses orang biasa,
  **bukan** pengaman tingkat server: orang yang paham devtools masih bisa
  melewati tampilannya. Endpoint data (`getSimulation`, dll.) juga tetap
  terbuka karena dipakai plugin Revit. **Jangan simpan data rahasia di sini.**
- Sesi berlaku 6 jam, lalu diminta login ulang.
- Bila server tidak terjangkau, login masih bisa dilakukan **offline** memakai
  kredensial terverifikasi dari login online terakhir di perangkat itu.

## Plugin Revit — unduh hasil build

Setiap push, GitHub Actions otomatis mengkompilasi plugin. Cara mengunduh:

1. Buka tab **Actions** di repo GitHub → pilih run terbaru **Build Revit Plugin**
2. Di bagian *Artifacts*, unduh **CableTrayHub-Revit2025** (zip)
3. Ekstrak lalu salin isinya ke `%APPDATA%\Autodesk\Revit\Addins\2025\`
   (petunjuk lengkap di [revit-plugin/README.md](revit-plugin/README.md))

## Cara Deploy (sekali saja)

### 1. Siapkan Google Sheets + Apps Script
1. Buka Google Sheets → buat/buka spreadsheet Anda.
2. Menu **Extensions → Apps Script**.
3. Salin isi `Code.gs` dan `Setup.gs` ke editor (file `index.html` juga bila ingin
   versi GAS-hosted).
4. **Spreadsheet baru:** jalankan fungsi `initSetup` sekali (membuat 3 sheet + data awal).
   **Spreadsheet lama yang sudah berisi data:** jalankan `upgradeSheets` sekali
   (hanya menambah kolom `Detail_JSON`, data tidak dihapus).

### 2. Deploy sebagai Web App
1. Klik **Deploy → New deployment → Web app**.
2. *Execute as:* **Me** · *Who has access:* **Anyone**.
3. Salin URL yang berakhiran `/exec` — ini adalah **API_URL** Anda.

> Setiap kali `Code.gs` diubah, buat **New deployment** lagi (atau Manage
> deployments → edit → versi baru) agar perubahan aktif di URL /exec.

### 3. Sambungkan index.html
1. Buka `index.html`, cari baris:
   ```js
   const API_URL = "";
   ```
2. Isi dengan URL /exec tadi:
   ```js
   const API_URL = "https://script.google.com/macros/s/XXXXX/exec";
   ```
3. Upload/deploy ulang `index.html` ke Vercel.

Selesai — sekarang riwayat simulasi tersimpan permanen di Google Sheets,
lintas user dan lintas komputer. Saat web dibuka akan muncul toast
"Terhubung ke database online". Jika API tidak terjangkau, web otomatis
jatuh ke mode offline (data bawaan).

## REST API (untuk plugin Revit)

Base URL = URL /exec Anda.

| Method | Request | Hasil |
|---|---|---|
| GET | `?action=ping` | Cek koneksi |
| GET | `?action=getInitialData` | Semua kabel, tray, dan riwayat |
| GET | `?action=getSimulation&id=SIM-20260717-0001` | 1 simulasi lengkap dengan `detail` |
| POST | body `{"action":"saveSimulation","data":{...}}` | Simpan riwayat baru |

Contoh respons `getSimulation` — field `detail` inilah yang dipakai plugin
Revit untuk menggambar kabel/conduit di cable tray yang di-select:

```json
{
  "success": true,
  "simulation": {
    "id": "SIM-20260717-0002",
    "namaProyek": "Gedung A - Panel A ke Panel B",
    "status": "AMAN",
    "detail": {
      "tray": { "jenis": "Perforated", "lebar": 300, "tinggi": 100, "maxLoad": 60 },
      "metode": "Flat Touching",
      "spare": "20%",
      "kabel": [
        { "id": "KAB-0001", "nama": "NYA 1.5 mm²", "tipeCore": "Single Core",
          "diameter": 3.1, "berat": 0.022, "qty": 1 }
      ]
    }
  }
}
```

Contoh pemanggilan dari C# (plugin Revit):

```csharp
using var http = new HttpClient();
var json = await http.GetStringAsync(
    API_URL + "?action=getSimulation&id=" + simId);
// deserialize dengan System.Text.Json / Newtonsoft, lalu:
// - user select cable tray (Panel A -> Panel B)
// - loop detail.kabel -> gambar conduit/kabel sesuai diameter & qty
```

Catatan: HttpClient mengikuti redirect Google secara default — jangan
dimatikan, karena Apps Script selalu me-redirect ke
`script.googleusercontent.com`.
