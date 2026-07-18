using Autodesk.Revit.UI;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace CableTrayHub.Revit
{
    /// <summary>
    /// Entry point add-in: membuat ribbon tab "Cable Tray Hub" dengan
    /// tombol PULL (website -> Revit) dan PUSH (Revit -> website).
    /// </summary>
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            const string tabName = "Cable Tray Hub";
            try { application.CreateRibbonTab(tabName); } catch { /* tab sudah ada */ }

            RibbonPanel panel = application.CreateRibbonPanel(tabName, "Website Sync");
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            var pullData = new PushButtonData(
                "PullSimulation",
                "Pull dari\nWebsite",
                assemblyPath,
                "CableTrayHub.Revit.PullCommand")
            {
                ToolTip = "Tarik data simulasi kabel dari website, lalu gambar/perbarui " +
                          "conduit di sepanjang cable tray tiap jalur (Panel Asal → Tujuan). " +
                          "Pull ulang otomatis meng-update conduit yang sudah ada."
            };

            var pushData = new PushButtonData(
                "PushStatus",
                "Push ke\nWebsite",
                assemblyPath,
                "CableTrayHub.Revit.PushCommand")
            {
                ToolTip = "Kirim balik status ke website: jalur mana yang sudah tergambar, " +
                          "jumlah conduit, dan total panjangnya."
            };

            pullData.LargeImage = LoadIcon("pull32.png");
            pullData.Image = LoadIcon("pull16.png");
            pushData.LargeImage = LoadIcon("push32.png");
            pushData.Image = LoadIcon("push16.png");

            panel.AddItem(pullData);
            panel.AddItem(pushData);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        /// <summary>
        /// Muat ikon PNG yang tertanam sebagai EmbeddedResource di DLL.
        /// null bila tidak ditemukan (tombol tampil tanpa ikon, tidak fatal).
        /// </summary>
        private static BitmapImage LoadIcon(string fileName)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using Stream s = asm.GetManifestResourceStream(
                    "CableTrayHub.Revit.Resources." + fileName);
                if (s == null) return null;

                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = s;
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch
            {
                return null;
            }
        }
    }
}
