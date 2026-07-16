using System.IO;
using System.Net.Http;
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
    }

    public class SimulationDetail
    {
        [JsonPropertyName("tray")] public TrayInfo Tray { get; set; }
        [JsonPropertyName("metode")] public string Metode { get; set; }
        [JsonPropertyName("spare")] public string Spare { get; set; }
        [JsonPropertyName("kabel")] public List<CableInfo> Kabel { get; set; } = new();
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
    }

    // ================= HTTP CLIENT =================

    public static class ApiClient
    {
        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            // Apps Script me-redirect ke script.googleusercontent.com — ikuti redirect.
            var handler = new HttpClientHandler { AllowAutoRedirect = true };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
            return client;
        }

        public static SimulationResponse GetSimulation(string apiUrl, string simId)
        {
            string url = apiUrl.TrimEnd('/') +
                         "?action=getSimulation&id=" + Uri.EscapeDataString(simId.Trim());
            string json = Http.GetStringAsync(url).GetAwaiter().GetResult();
            return JsonSerializer.Deserialize<SimulationResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }

    // ================= KONFIGURASI LOKAL (ingat URL & ID terakhir) =================

    public class PluginConfig
    {
        public string ApiUrl { get; set; } = "";
        public string LastSimulationId { get; set; } = "";

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
