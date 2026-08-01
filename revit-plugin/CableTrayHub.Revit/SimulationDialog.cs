using System.Drawing;
using System.Windows.Forms;
// Alias tipe Revit yang dipakai — hindari `using Autodesk.Revit.DB;` penuh
// karena bentrok dengan System.Windows.Forms (Form) & System.Drawing (Color).
using Document = Autodesk.Revit.DB.Document;
using ElementId = Autodesk.Revit.DB.ElementId;
using FilteredElementCollector = Autodesk.Revit.DB.FilteredElementCollector;
using FilteredWorksetCollector = Autodesk.Revit.DB.FilteredWorksetCollector;
using Workset = Autodesk.Revit.DB.Workset;
using WorksetKind = Autodesk.Revit.DB.WorksetKind;
using WorksetId = Autodesk.Revit.DB.WorksetId;
using ConduitType = Autodesk.Revit.DB.Electrical.ConduitType;

namespace CableTrayHub.Revit
{
    /// <summary>
    /// Dialog PULL: isi URL API + ID simulasi -> ambil data dari website ->
    /// pilih Tipe conduit & Workset -> OK untuk lanjut sinkronisasi ke model.
    /// </summary>
    public class SimulationDialog : Form
    {
        /// <summary>Item combo: teks tampil + nilai (ElementId/WorksetId).</summary>
        private class ComboItem
        {
            public string Text;
            public object Value;
            public override string ToString() => Text;
        }

        private readonly TextBox _urlBox;
        private readonly TextBox _idBox;
        private readonly Button _fetchButton;
        private readonly Button _okButton;
        private readonly Button _cancelButton;
        private readonly ListBox _routeList;
        private readonly Label _statusLabel;
        private readonly NumericUpDown _bottomClrBox;
        private readonly NumericUpDown _sideClrBox;
        private readonly ComboBox _conduitTypeBox;
        private readonly ComboBox _worksetBox;

        public Simulation Result { get; private set; }
        public string ApiUrl => _urlBox.Text.Trim();
        public string SimulationId => _idBox.Text.Trim();
        public double BottomClearanceMm => (double)_bottomClrBox.Value;
        public double SideClearanceMm => (double)_sideClrBox.Value;

        /// <summary>ConduitType terpilih (InvalidElementId bila tak ada).</summary>
        public ElementId SelectedConduitTypeId =>
            (_conduitTypeBox.SelectedItem as ComboItem)?.Value as ElementId ?? ElementId.InvalidElementId;
        public string SelectedConduitTypeName =>
            (_conduitTypeBox.SelectedItem as ComboItem)?.Text ?? "";

        /// <summary>Workset terpilih (null bila model tidak workshared).</summary>
        public WorksetId SelectedWorksetId =>
            (_worksetBox.SelectedItem as ComboItem)?.Value as WorksetId;
        public string SelectedWorksetName =>
            ((_worksetBox.SelectedItem as ComboItem)?.Value as WorksetId) != null
                ? (_worksetBox.SelectedItem as ComboItem).Text : "";

        public SimulationDialog(PluginConfig config, Document doc)
        {
            Text = "Cable Tray Hub — Pull Simulasi dari Website";
            Width = 600;
            Height = 600;
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

            _routeList = new ListBox { Left = 15, Top = 168, Width = 550, Height = 180 };

            // Jarak aman conduit terhadap tray (mm) — diingat antar sesi.
            var clrLabel = new Label
            {
                Text = "Jarak aman conduit (mm):", Left = 15, Top = 362, Width = 160
            };
            var bottomClrLabel = new Label
            {
                Text = "ke dasar tray", Left = 180, Top = 362, Width = 85,
                TextAlign = ContentAlignment.MiddleRight
            };
            _bottomClrBox = new NumericUpDown
            {
                Left = 270, Top = 358, Width = 70,
                Minimum = 0, Maximum = 500, DecimalPlaces = 0, Increment = 5,
                Value = (decimal)Math.Clamp(config.BottomClearanceMm, 0, 500)
            };
            var sideClrLabel = new Label
            {
                Text = "ke arm samping", Left = 350, Top = 362, Width = 100,
                TextAlign = ContentAlignment.MiddleRight
            };
            _sideClrBox = new NumericUpDown
            {
                Left = 455, Top = 358, Width = 70,
                Minimum = 0, Maximum = 500, DecimalPlaces = 0, Increment = 5,
                Value = (decimal)Math.Clamp(config.SideClearanceMm, 0, 500)
            };

            // Tipe conduit: daftar semua ConduitType di proyek.
            var typeLabel = new Label { Text = "Tipe conduit:", Left = 15, Top = 396, Width = 110 };
            _conduitTypeBox = new ComboBox
            {
                Left = 130, Top = 393, Width = 435, DropDownStyle = ComboBoxStyle.DropDownList
            };
            PopulateConduitTypes(doc, config.LastConduitType);

            // Workset: hanya bila model workshared.
            var worksetLabel = new Label { Text = "Workset:", Left = 15, Top = 428, Width = 110 };
            _worksetBox = new ComboBox
            {
                Left = 130, Top = 425, Width = 435, DropDownStyle = ComboBoxStyle.DropDownList
            };
            PopulateWorksets(doc, config.LastWorkset);

            _okButton = new Button
            {
                Text = "Lanjut: Sinkronkan ke Model ➜",
                Left = 255, Top = 470, Width = 210, Height = 32,
                Enabled = false, DialogResult = DialogResult.OK
            };
            _cancelButton = new Button
            {
                Text = "Batal", Left = 475, Top = 470, Width = 90, Height = 32,
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange(new Control[]
            {
                urlLabel, _urlBox, idLabel, _idBox, _fetchButton,
                _statusLabel, _routeList,
                clrLabel, bottomClrLabel, _bottomClrBox, sideClrLabel, _sideClrBox,
                typeLabel, _conduitTypeBox, worksetLabel, _worksetBox,
                _okButton, _cancelButton
            });

            AcceptButton = _okButton;
            CancelButton = _cancelButton;
        }

        /// <summary>Isi dropdown Tipe conduit ("Family: Type"), pilih yang tersimpan.</summary>
        private void PopulateConduitTypes(Document doc, string lastSelected)
        {
            var items = new List<ComboItem>();
            foreach (ConduitType ct in new FilteredElementCollector(doc)
                         .OfClass(typeof(ConduitType)).Cast<ConduitType>())
            {
                items.Add(new ComboItem { Text = ct.FamilyName + ": " + ct.Name, Value = ct.Id });
            }
            items.Sort((a, b) => string.Compare(a.Text, b.Text, System.StringComparison.OrdinalIgnoreCase));

            if (items.Count == 0)
            {
                _conduitTypeBox.Items.Add(new ComboItem { Text = "(tidak ada Conduit Type di proyek)", Value = null });
                _conduitTypeBox.SelectedIndex = 0;
                _conduitTypeBox.Enabled = false;
                return;
            }

            foreach (var it in items) _conduitTypeBox.Items.Add(it);
            int idx = items.FindIndex(i => i.Text == lastSelected);
            _conduitTypeBox.SelectedIndex = idx >= 0 ? idx : 0;
        }

        /// <summary>Isi dropdown Workset (hanya user workset), pilih yang tersimpan.</summary>
        private void PopulateWorksets(Document doc, string lastSelected)
        {
            if (!doc.IsWorkshared)
            {
                _worksetBox.Items.Add(new ComboItem { Text = "(model tidak workshared)", Value = null });
                _worksetBox.SelectedIndex = 0;
                _worksetBox.Enabled = false;
                return;
            }

            var items = new List<ComboItem>();
            foreach (Workset ws in new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset))
                items.Add(new ComboItem { Text = ws.Name, Value = ws.Id });
            items.Sort((a, b) => string.Compare(a.Text, b.Text, System.StringComparison.OrdinalIgnoreCase));

            foreach (var it in items) _worksetBox.Items.Add(it);
            int idx = items.FindIndex(i => i.Text == lastSelected);
            _worksetBox.SelectedIndex = idx >= 0 ? idx : 0;
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
                string metode = string.IsNullOrEmpty(sim.Detail?.Metode) ? "Flat Touching" : sim.Detail.Metode;
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
                                    $"total {totalConduit} conduit  •  Metode: {metode}";
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
