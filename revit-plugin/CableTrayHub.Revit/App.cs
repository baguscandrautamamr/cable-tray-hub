using Autodesk.Revit.UI;
using System.Reflection;

namespace CableTrayHub.Revit
{
    /// <summary>
    /// Entry point add-in: membuat ribbon tab "Cable Tray Hub" dengan
    /// tombol untuk mengimpor simulasi dari website ke model Revit.
    /// </summary>
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            const string tabName = "Cable Tray Hub";
            try { application.CreateRibbonTab(tabName); } catch { /* tab sudah ada */ }

            RibbonPanel panel = application.CreateRibbonPanel(tabName, "Website Sync");

            var buttonData = new PushButtonData(
                "ImportSimulation",
                "Import\nSimulasi",
                Assembly.GetExecutingAssembly().Location,
                "CableTrayHub.Revit.ImportSimulationCommand")
            {
                ToolTip = "Ambil data simulasi kabel dari website Cable Tray Hub, " +
                          "lalu gambar conduit di sepanjang cable tray yang dipilih."
            };

            panel.AddItem(buttonData);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
