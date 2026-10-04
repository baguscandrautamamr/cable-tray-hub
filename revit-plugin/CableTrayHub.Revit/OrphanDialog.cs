using System.Drawing;
using System.Windows.Forms;

namespace CableTrayHub.Revit
{
    /// <summary>
    /// Dialog konfirmasi: daftar jalur LAMA di model (conduit bertanda CTH|)
    /// yang tidak ada di simulasi ini tapi memakai panel yang sama — biasanya
    /// akibat nama panel diganti/dikoreksi, atau jalur dihapus di website.
    /// User mencentang jalur yang conduit-nya ingin dihapus.
    /// </summary>
    public class OrphanDialog : Form
    {
        public class Item
        {
            public string Key;
            public int Count;
            public string Reason;
            public bool Checked;
            public override string ToString() => $"{Key}  —  {Count} elemen  ({Reason})";
        }

        private readonly CheckedListBox _list;
        public List<string> SelectedKeys { get; } = new();

        public OrphanDialog(List<Item> items)
        {
            Text = "Cable Tray Hub — Conduit Lama Terdeteksi";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(620, 340);
            Font = new Font("Segoe UI", 9f);

            var info = new Label
            {
                Left = 12, Top = 10, Width = 596, Height = 48,
                Text = "Ditemukan conduit dari jalur yang TIDAK ada di simulasi ini, tetapi memakai " +
                       "panel yang sama (kemungkinan nama panel diganti atau jalur dihapus di website). " +
                       "Centang jalur yang conduit-nya ingin DIHAPUS sebelum digambar ulang:"
            };
            _list = new CheckedListBox { Left = 12, Top = 62, Width = 596, Height = 220, CheckOnClick = true };
            foreach (var it in items) _list.Items.Add(it, it.Checked);

            var ok = new Button { Text = "Lanjut", Left = 418, Top = 296, Width = 92, DialogResult = DialogResult.OK };
            var skip = new Button { Text = "Lewati", Left = 516, Top = 296, Width = 92, DialogResult = DialogResult.Cancel };
            ok.Click += (s, e) =>
            {
                foreach (var o in _list.CheckedItems) SelectedKeys.Add(((Item)o).Key);
            };

            Controls.Add(info);
            Controls.Add(_list);
            Controls.Add(ok);
            Controls.Add(skip);
            AcceptButton = ok;
            CancelButton = skip;
        }
    }
}
