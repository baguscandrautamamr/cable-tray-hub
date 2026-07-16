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
| `Code.gs` | Backend Apps Script: web app + REST API JSON |
| `Setup.gs` | Pembuat tabel database di Google Sheets (`initSetup`) + upgrade tanpa hapus data (`upgradeSheets`) |
| `index.html` | Frontend — bisa di-host di Apps Script ATAU Vercel |

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
