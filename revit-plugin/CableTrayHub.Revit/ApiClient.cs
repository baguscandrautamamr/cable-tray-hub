using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CableTrayHub.Revit
{
    // ================= MODEL DATA (sesuai respons API Apps Script) =================

    public class SimulationResponse
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("error")] public string Error { get; set; }
        [JsonPropertyName("simulation")] public Simulation Simulation { get; set; }
    }

    public class Simulation
    {
        [JsonPropertyName("id")] public string Id { get; set; }
        [JsonPropertyName("tanggal")] public string Tanggal { get; set; }
        [JsonPropertyName("namaProyek")] public string NamaProyek { get; set; }
        [JsonPropertyName("jenisTray")] public string JenisTray { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; }
        [JsonPropertyName("detail")] public SimulationDetail Detail { get; set; }

        /// <summary>
        /// Daftar jalur ternormalisasi: versi web baru menyimpan detail.routes,
        /// versi lama menyimpan detail.kabel (tanpa jalur) — keduanya didukung.
        /// </summary>
        public List<RouteInfo> GetRoutes()
        {
            if (Detail == null) return new List<RouteInfo>();

            if (Detail.Routes != null && Detail.Routes.Count > 0)
            {
                var list = Detail.Routes.Where(r => r.Kabel != null && r.Kabel.Count > 0).ToList();
                foreach (var r in list) r.Normalize();
                return list;
            }

            if (Detail.Kabel != null && Detail.Kabel.Count > 0)
            {
                return new List<RouteInfo>
                {
                    new RouteInfo
                    {
                        Key = "JALUR-UTAMA",
                        PanelFrom = "Panel Asal",
                        PanelTo = "Panel Tujuan",
                        Kabel = Detail.Kabel
                    }
                };
            }
            return new List<RouteInfo>();
        }
    }

    public class SimulationDetail
    {
        [JsonPropertyName("tray")] public TrayInfo Tray { get; set; }
        [JsonPropertyName("metode")] public string Metode { get; set; }
        [JsonPropertyName("spare")] public string Spare { get; set; }
        [JsonPropertyName("routes")] public List<RouteInfo> Routes { get; set; }
        [JsonPropertyName("kabel")] public List<CableInfo> Kabel { get; set; } // format lama
    }

    public class RouteInfo
    {
        [JsonPropertyName("key")] public string Key { get; set; }
        [JsonPropertyName("panelFrom")] public string PanelFrom { get; set; }
        [JsonPropertyName("panelTo")] public string PanelTo { get; set; }
        [JsonPropertyName("kabel")] public List<CableInfo> Kabel { get; set; } = new();

        /// <summary>Rapikan spasi nama panel (depan/belakang/ganda) lalu susun ulang Key.</summary>
        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(PanelFrom) && string.IsNullOrWhiteSpace(PanelTo) && Key != null)
            {
                int a = Key.IndexOf('→');
                if (a >= 0) { PanelFrom = Key.Substring(0, a); PanelTo = Key.Substring(a + 1); }
            }
            PanelFrom = CleanName(PanelFrom);
            PanelTo = CleanName(PanelTo);
            if (PanelFrom.Length > 0 || PanelTo.Length > 0)
                Key = (PanelFrom.Length > 0 ? PanelFrom : "Asal") + "→" + (PanelTo.Length > 0 ? PanelTo : "Tujuan");
            else
                Key = CleanName(Key);
        }

        public static string CleanName(string s) =>
            string.Join(" ", (s ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries));

        /// <summary>
        /// Kunci pembanding jalur: spasi dirapikan (termasuk di sekitar "→")
        /// dan huruf besar/kecil diabaikan. "lp-ground  floor " == "LP-GROUND FLOOR".
        /// </summary>
        public static string NormKey(string key)
        {
            key = key ?? "";
            int a = key.IndexOf('→');
            string k = a >= 0 ? CleanName(key.Substring(0, a)) + "→" + CleanName(key.Substring(a + 1))
                              : CleanName(key);
            return k.ToUpperInvariant();
        }
    }

    public class TrayInfo
    {
        [JsonPropertyName("jenis")] public string Jenis { get; set; }
        [JsonPropertyName("lebar")] public double Lebar { get; set; }
        [JsonPropertyName("tinggi")] public double Tinggi { get; set; }
        [JsonPropertyName("maxLoad")] public double MaxLoad { get; set; }
    }

    public class CableInfo
    {
        [JsonPropertyName("id")] public string Id { get; set; }
        [JsonPropertyName("nama")] public string Nama { get; set; }
        [JsonPropertyName("tipeCore")] public string TipeCore { get; set; }
        [JsonPropertyName("diameter")] public double Diameter { get; set; } // mm
        [JsonPropertyName("berat")] public double Berat { get; set; }       // kg/m
        [JsonPropertyName("qty")] public int Qty { get; set; }

        /// <summary>
        /// Posisi tiap kabel pada penampang tray dari kanvas visual website
        /// (satu entri per qty): x dari dinding kiri tray, y dari dasar tray,
        /// dalam mm, titik pusat kabel. Mengikuti metode konfigurasi
        /// (Flat Touching/Spaced/Trefoil) + hasil drag manual user.
        /// </summary>
        [JsonPropertyName("posisi")] public List<PosXY> Posisi { get; set; }
    }

    public class PosXY
    {
        [JsonPropertyName("x")] public double X { get; set; } // mm dari dinding kiri
        [JsonPropertyName("y")] public double Y { get; set; } // mm dari dasar tray
    }

    // ================= PAYLOAD PUSH KE WEBSITE =================

    public class PushPayload
    {
        [JsonPropertyName("simId")] public string SimId { get; set; }
        [JsonPropertyName("model")] public string Model { get; set; }
        [JsonPropertyName("user")] public string User { get; set; }
        [JsonPropertyName("routes")] public List<PushRoute> Routes { get; set; } = new();
    }

    public class PushRoute
    {
        [JsonPropertyName("key")] public string Key { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; }
        [JsonPropertyName("jumlahConduit")] public int JumlahConduit { get; set; }
        [JsonPropertyName("totalPanjangM")] public double TotalPanjangM { get; set; }
    }

    public class PushResponse
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("error")] public string Error { get; set; }
        [JsonPropertyName("saved")] public int Saved { get; set; }
    }

    // ================= HTTP CLIENT =================

    public static class ApiClient
    {
        private static readonly HttpClient Http = CreateClient();

        private static readonly JsonSerializerOptions JsonOpts =
            new() { PropertyNameCaseInsensitive = true };

        private static HttpClient CreateClient()
        {
            // Apps Script me-redirect ke script.googleusercontent.com — ikuti redirect.
            var handler = new HttpClientHandler { AllowAutoRedirect = true };
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        }

        public static SimulationResponse GetSimulation(string apiUrl, string simId)
        {
            string url = apiUrl.TrimEnd('/') +
                         "?action=getSimulation&id=" + Uri.EscapeDataString(simId.Trim());
            string json = Http.GetStringAsync(url).GetAwaiter().GetResult();
            return JsonSerializer.Deserialize<SimulationResponse>(json, JsonOpts);
        }

        public static PushResponse PushStatus(string apiUrl, PushPayload payload)
        {
            var body = new { action = "pushRevitStatus", data = payload };
            var content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "text/plain");
            var response = Http.PostAsync(apiUrl.TrimEnd('/'), content).GetAwaiter().GetResult();
            string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonSerializer.Deserialize<PushResponse>(json, JsonOpts);
        }
    }

    // ================= KONFIGURASI LOKAL (ingat URL & ID terakhir) =================

    public class PluginConfig
    {
        public string ApiUrl { get; set; } = "";
        public string LastSimulationId { get; set; } = "";
        // Jarak aman conduit terhadap tray (mm) — bisa diatur dari dialog Pull.
        public double BottomClearanceMm { get; set; } = 10;
        public double SideClearanceMm { get; set; } = 10;
        // Pilihan terakhir di dialog Pull (diingat antar sesi).
        public string LastConduitType { get; set; } = ""; // "Family: Type"
        public string LastWorkset { get; set; } = "";      // nama workset

        private static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CableTrayHub", "config.json");

        public static PluginConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    return JsonSerializer.Deserialize<PluginConfig>(File.ReadAllText(ConfigPath))
                           ?? new PluginConfig();
                }
            }
            catch { /* konfigurasi korup -> pakai default */ }
            return new PluginConfig();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* gagal simpan config bukan error fatal */ }
        }
    }
}
