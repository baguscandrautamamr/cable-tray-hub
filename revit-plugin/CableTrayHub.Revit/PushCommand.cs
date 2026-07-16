using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using System.Text;
// Revit UI dan WinForms sama-sama punya class TaskDialog — pakai versi Revit
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace CableTrayHub.Revit
{
    /// <summary>
    /// PUSH (Revit -> website): mengumpulkan semua conduit bertanda
    /// "CTH|jalur|kabel" di model, merangkum per jalur (jumlah conduit dan
    /// total panjang), lalu mengirimkannya ke database website
    /// (tercatat di sheet Revit_Sync). Engineer di website jadi tahu
    /// jalur mana yang sudah dieksekusi di model.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class PushCommand : IExternalCommand
    {
        private const double FtToM = 0.3048;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            var config = PluginConfig.Load();

            if (string.IsNullOrWhiteSpace(config.ApiUrl))
            {
                TaskDialog.Show("Cable Tray Hub",
                    "URL API belum diatur. Jalankan \"Pull dari Website\" sekali " +
                    "untuk mengisi URL API terlebih dahulu.");
                return Result.Cancelled;
            }

            // ---------- 1. Rangkum conduit bertanda per jalur ----------
            var perRoute = new Dictionary<string, PushRoute>();

            var conduits = new FilteredElementCollector(doc).OfClass(typeof(Conduit));
            foreach (Element e in conduits)
            {
                string tag = PullCommand.GetComments(e);
                if (!tag.StartsWith(PullCommand.TagPrefix)) continue;

                // Format: CTH|<jalur>|<nama kabel>
                string[] parts = tag.Split('|');
                if (parts.Length < 3) continue;
                string routeKey = parts[1];

                if (!perRoute.TryGetValue(routeKey, out PushRoute r))
                {
                    r = new PushRoute { Key = routeKey, Status = "TERGAMBAR DI REVIT" };
                    perRoute[routeKey] = r;
                }

                r.JumlahConduit++;
                if (e.Location is LocationCurve lc)
                {
                    r.TotalPanjangM += lc.Curve.Length * FtToM;
                }
            }

            if (perRoute.Count == 0)
            {
                TaskDialog.Show("Cable Tray Hub",
                    "Tidak ditemukan conduit hasil Pull di model ini.\n" +
                    "Jalankan \"Pull dari Website\" terlebih dahulu.");
                return Result.Cancelled;
            }

            foreach (var r in perRoute.Values)
                r.TotalPanjangM = Math.Round(r.TotalPanjangM, 2);

            // ---------- 2. Konfirmasi ----------
            var preview = new StringBuilder();
            foreach (var r in perRoute.Values)
                preview.AppendLine($"• {r.Key}: {r.JumlahConduit} conduit, {r.TotalPanjangM} m");

            var confirm = new TaskDialog("Cable Tray Hub — Push ke Website")
            {
                MainInstruction = "Kirim status berikut ke database website?",
                MainContent = $"Simulasi: {config.LastSimulationId}\nModel: {doc.Title}\n\n{preview}",
                CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                DefaultButton = TaskDialogResult.Yes
            };
            if (confirm.Show() != TaskDialogResult.Yes) return Result.Cancelled;

            // ---------- 3. Kirim ----------
            var payload = new PushPayload
            {
                SimId = config.LastSimulationId,
                Model = doc.Title,
                User = Environment.UserName,
                Routes = perRoute.Values.ToList()
            };

            try
            {
                PushResponse response = ApiClient.PushStatus(config.ApiUrl, payload);
                if (response != null && response.Success)
                {
                    TaskDialog.Show("Cable Tray Hub",
                        $"Push berhasil! {response.Saved} jalur tercatat di sheet Revit_Sync " +
                        "pada database website.");
                    return Result.Succeeded;
                }

                message = "Server menolak: " + (response?.Error ?? "tanpa respons");
                return Result.Failed;
            }
            catch (Exception ex)
            {
                message = "Gagal terhubung ke website: " + ex.Message;
                return Result.Failed;
            }
        }
    }
}
