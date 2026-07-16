using System.Drawing;
using System.Windows.Forms;

namespace CableTrayHub.Revit
{
    /// <summary>
    /// Dialog PULL: isi URL API + ID simulasi -> ambil data dari website ->
    /// tampilkan ringkasan per jalur -> OK untuk lanjut sinkronisasi ke model.
    /// </summary>
    public class SimulationDialog : Form
    {
        private readonly TextBox _urlBox;
        private readonly TextBox _idBox;
        private readonly Button _fetchButton;
        private readonly Button _okButton;
        private readonly Button _cancelButton;
        private readonly ListBox _routeList;
        private readonly Label _statusLabel;

        public Simulation Result { get; private set; }
        public string ApiUrl => _urlBox.Text.Trim();
        public string SimulationId => _idBox.Text.Trim();

        public SimulationDialog(PluginConfig config)
        {
            Text = "Cable Tray Hub — Pull Simulasi dari Website";
            Width = 600;
            Height = 500;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            var urlLabel = new Label { Text = "URL API (Apps Script /exec):", Left = 15, Top = 15, Width = 550 };
            _urlBox = new TextBox { Left = 15, Top = 38, Width = 550, Text = config.ApiUrl };

            var idLabel = new Label { Text = "ID Simulasi (dari tombol \"Connect ke Revit\" di website):", Left = 15, Top = 70, Width = 390 };
            _idBox = new TextBox { Left = 15, Top = 93, Width = 390, Text = config.LastSimulationId };

            _fetchButton = new Button { Text = "Ambil Data", Left = 420, Top = 91, Width = 145, Height = 28 };
            _fetchButton.Click += (s, e) => FetchData();

            _statusLabel = new Label
            {
                Text = "Masukkan URL API dan ID simulasi, lalu klik \"Ambil Data\".",
                Left = 15, Top = 130, Width = 550, Height = 34, ForeColor = Color.DimGray
            };

            _routeList = new ListBox { Left = 15, Top = 168, Width = 550, Height = 220 };

            _okButton = new Button
            {
                Text = "Lanjut: Sinkronkan ke Model ➜",
                Left = 255, Top = 405, Width = 210, Height = 32,
                Enabled = false, DialogResult = DialogResult.OK
            };
            _cancelButton = new Button
            {
                Text = "Batal", Left = 475, Top = 405, Width = 90, Height = 32,
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange(new Control[]
            {
                urlLabel, _urlBox, idLabel, _idBox, _fetchButton,
                _statusLabel, _routeList, _okButton, _cancelButton
            });

            AcceptButton = _okButton;
            CancelButton = _cancelButton;
        }

        private void FetchData()
        {
            if (string.IsNullOrWhiteSpace(ApiUrl) || string.IsNullOrWhiteSpace(SimulationId))
            {
                _statusLabel.Text = "URL API dan ID simulasi wajib diisi.";
                _statusLabel.ForeColor = Color.Firebrick;
                return;
            }

            _fetchButton.Enabled = false;
            _statusLabel.Text = "Mengambil data dari website...";
            _statusLabel.ForeColor = Color.DimGray;
            _routeList.Items.Clear();
            Result = null;
            _okButton.Enabled = false;
            Cursor = Cursors.WaitCursor;

            try
            {
                var response = ApiClient.GetSimulation(ApiUrl, SimulationId);

                if (response == null || !response.Success || response.Simulation == null)
                {
                    _statusLabel.Text = "Gagal: " + (response?.Error ?? "respons tidak dikenali.");
                    _statusLabel.ForeColor = Color.Firebrick;
                    return;
                }

                var sim = response.Simulation;
                var routes = sim.GetRoutes();
                if (routes.Count == 0)
                {
                    _statusLabel.Text = "Simulasi ditemukan tapi tidak punya detail jalur/kabel. " +
                                        "Simpan ulang dari website versi terbaru.";
                    _statusLabel.ForeColor = Color.Firebrick;
                    return;
                }

                Result = sim;
                int totalConduit = 0;
                foreach (var r in routes)
                {
                    int n = r.Kabel.Sum(k => k.Qty);
                    totalConduit += n;
                    _routeList.Items.Add($"◼ JALUR: {r.Key}   ({n} conduit)");
                    foreach (var k in r.Kabel)
                        _routeList.Items.Add($"      {k.Nama}  |  De {k.Diameter} mm  |  {k.Qty} jalur");
                }

                _statusLabel.Text = $"✔ {sim.NamaProyek} [{sim.Status}] — {routes.Count} jalur, " +
                                    $"total {totalConduit} conduit akan disinkronkan.";
                _statusLabel.ForeColor = Color.ForestGreen;
                _okButton.Enabled = true;
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Error koneksi: " + ex.Message;
                _statusLabel.ForeColor = Color.Firebrick;
            }
            finally
            {
                _fetchButton.Enabled = true;
                Cursor = Cursors.Default;
            }
        }
    }
}
