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
        private readonly NumericUpDown _bottomClrBox;
        private readonly NumericUpDown _sideClrBox;
        private readonly ComboBox _typeCombo;
        private readonly ComboBox _wsCombo;
        private readonly bool _hasWorksets;

        public Simulation Result { get; private set; }
        public string ApiUrl => _urlBox.Text.Trim();
        public string SimulationId => _idBox.Text.Trim();
        public double BottomClearanceMm => (double)_bottomClrBox.Value;
        public double SideClearanceMm => (double)_sideClrBox.Value;
        public string ConduitTypeName => _typeCombo.SelectedItem?.ToString() ?? "";
        public string WorksetName => _hasWorksets ? (_wsCombo.SelectedItem?.ToString() ?? "") : "";

        /// <param name="conduitTypes">Nama semua ConduitType di project.</param>
        /// <param name="defaultConduitType">Tipe rekomendasi (punya aturan elbow).</param>
        /// <param name="worksets">Nama user workset; kosong bila model tidak workshared.</param>
        /// <param name="defaultWorkset">Workset aktif saat ini.</param>
        public SimulationDialog(PluginConfig config,
            IList<string> conduitTypes, string defaultConduitType,
            IList<string> worksets, string defaultWorkset)
        {
            Text = "Cable Tray Hub — Pull Simulasi dari Website";
            Width = 600;
            Height = 632;
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

            // Jarak aman conduit terhadap tray (mm) — diingat antar sesi.
            var clrLabel = new Label
            {
                Text = "Jarak aman conduit (mm):", Left = 15, Top = 402, Width = 160
            };
            var bottomClrLabel = new Label
            {
                Text = "ke dasar tray", Left = 180, Top = 402, Width = 85,
                TextAlign = ContentAlignment.MiddleRight
            };
            _bottomClrBox = new NumericUpDown
            {
                Left = 270, Top = 398, Width = 70,
                Minimum = 0, Maximum = 500, DecimalPlaces = 0, Increment = 5,
                Value = (decimal)Math.Clamp(config.BottomClearanceMm, 0, 500)
            };
            var sideClrLabel = new Label
            {
                Text = "ke arm samping", Left = 350, Top = 402, Width = 100,
                TextAlign = ContentAlignment.MiddleRight
            };
            _sideClrBox = new NumericUpDown
            {
                Left = 455, Top = 398, Width = 70,
                Minimum = 0, Maximum = 500, DecimalPlaces = 0, Increment = 5,
                Value = (decimal)Math.Clamp(config.SideClearanceMm, 0, 500)
            };

            // Tipe conduit yang dipakai menggambar + workset tujuan elemen baru
            // — masing-masing satu baris penuh agar nama panjang tetap terbaca.
            var typeLabel = new Label { Text = "Tipe conduit:", Left = 15, Top = 436, Width = 82 };
            _typeCombo = new ComboBox
            {
                Left = 100, Top = 432, Width = 465,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (string t in conduitTypes) _typeCombo.Items.Add(t);
            SelectPreferred(_typeCombo, config.ConduitTypeName, defaultConduitType);
            if (_typeCombo.Items.Count == 0)
            {
                _typeCombo.Items.Add("(tidak ada Conduit Type di project)");
                _typeCombo.SelectedIndex = 0;
                _typeCombo.Enabled = false;
            }

            var wsLabel = new Label { Text = "Workset:", Left = 15, Top = 470, Width = 82 };
            _hasWorksets = worksets != null && worksets.Count > 0;
            _wsCombo = new ComboBox
            {
                Left = 100, Top = 466, Width = 465,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            if (_hasWorksets)
            {
                foreach (string w in worksets) _wsCombo.Items.Add(w);
                SelectPreferred(_wsCombo, config.WorksetName, defaultWorkset);
            }
            else
            {
                _wsCombo.Items.Add("(model tanpa workset)");
                _wsCombo.SelectedIndex = 0;
                _wsCombo.Enabled = false;
            }

            _okButton = new Button
            {
                Text = "Lanjut: Sinkronkan ke Model ➜",
                Left = 255, Top = 510, Width = 210, Height = 32,
                Enabled = false, DialogResult = DialogResult.OK
            };
            _cancelButton = new Button
            {
                Text = "Batal", Left = 475, Top = 510, Width = 90, Height = 32,
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange(new Control[]
            {
                urlLabel, _urlBox, idLabel, _idBox, _fetchButton,
                _statusLabel, _routeList,
                clrLabel, bottomClrLabel, _bottomClrBox, sideClrLabel, _sideClrBox,
                typeLabel, _typeCombo, wsLabel, _wsCombo,
                _okButton, _cancelButton
            });

            AcceptButton = _okButton;
            CancelButton = _cancelButton;
        }

        /// <summary>
        /// Pilih item combo: pilihan tersimpan di config bila masih ada di
        /// project, kalau tidak pakai default, kalau tidak item pertama.
        /// </summary>
        private static void SelectPreferred(ComboBox combo, string saved, string fallback)
        {
            if (combo.Items.Count == 0) return;
            if (!string.IsNullOrEmpty(saved) && combo.Items.Contains(saved))
                combo.SelectedItem = saved;
            else if (!string.IsNullOrEmpty(fallback) && combo.Items.Contains(fallback))
                combo.SelectedItem = fallback;
            else
                combo.SelectedIndex = 0;
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
