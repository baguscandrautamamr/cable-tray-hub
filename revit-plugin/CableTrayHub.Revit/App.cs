using Autodesk.Revit.UI;
using System.Reflection;

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

            panel.AddItem(pullData);
            panel.AddItem(pushData);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
