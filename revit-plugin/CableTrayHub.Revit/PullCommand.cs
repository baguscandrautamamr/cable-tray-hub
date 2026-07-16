using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Text;
using System.Windows.Forms;
// Revit UI dan WinForms sama-sama punya class TaskDialog — pakai versi Revit
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace CableTrayHub.Revit
{
    /// <summary>
    /// PULL (website -> Revit), per jalur Panel Asal → Panel Tujuan:
    /// 1. Ambil simulasi dari database website.
    /// 2. Jalur baru  : user select cable tray sekali -> conduit digambar,
    ///    pilihan tray diingat di dalam file Revit.
    /// 3. Jalur lama  : conduit bertanda jalur itu DIHAPUS lalu digambar ulang
    ///    sesuai data website terbaru (sinkron otomatis, tanpa duplikat),
    ///    tanpa perlu select tray lagi.
    /// Penanda: parameter Comments tiap conduit = "CTH|<jalur>|<nama kabel>".
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class PullCommand : IExternalCommand
    {
        internal const string TagPrefix = "CTH|";
        private const double MmToFt = 1.0 / 304.8;
        // Dua segmen tray dianggap tersambung bila ujungnya berjarak < 60 cm
        // (memberi toleransi untuk fitting/elbow di antara segmen).
        private const double JoinToleranceFt = 600 * MmToFt;
        private const double MinSegmentFt = 50 * MmToFt;

        private class RoutePlan
        {
            public RouteInfo Route;
            public List<CableTray> Trays = new();
            public bool NewSelection;
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // ---------- 1. Dialog: ambil data simulasi dari website ----------
            var config = PluginConfig.Load();
            Simulation sim;
            using (var dialog = new SimulationDialog(config))
            {
                if (dialog.ShowDialog() != DialogResult.OK || dialog.Result == null)
                    return Result.Cancelled;

                sim = dialog.Result;
                config.ApiUrl = dialog.ApiUrl;
                config.LastSimulationId = dialog.SimulationId;
                config.Save();
            }

            var routes = sim.GetRoutes();
            var mapping = SyncStorage.Load(doc);

            // ---------- 2. Tentukan tray tiap jalur (di luar transaction) ----------
            var plans = new List<RoutePlan>();
            var skipped = new List<string>();

            foreach (var route in routes)
            {
                var plan = new RoutePlan { Route = route };

                // Coba pakai pilihan tray yang tersimpan dari pull sebelumnya
                if (mapping.TryGetValue(route.Key, out List<string> uniqueIds))
                {
                    foreach (string uid in uniqueIds)
                    {
                        if (doc.GetElement(uid) is CableTray tray) plan.Trays.Add(tray);
                    }
                    if (plan.Trays.Count != uniqueIds.Count) plan.Trays.Clear(); // ada tray yang hilang -> select ulang
                }

                if (plan.Trays.Count == 0)
                {
                    TaskDialog.Show("Cable Tray Hub",
                        $"JALUR: {route.Key}\n\nSetelah menutup dialog ini, pilih segmen-segmen " +
                        $"cable tray dari \"{route.PanelFrom}\" ke \"{route.PanelTo}\", lalu klik Finish.\n" +
                        "(Tekan Esc untuk melewati jalur ini.)");
                    try
                    {
                        IList<Reference> refs = uidoc.Selection.PickObjects(
                            ObjectType.Element, new CableTrayFilter(),
                            $"Pilih cable tray jalur {route.Key}, lalu klik Finish");
                        foreach (var r in refs)
                        {
                            if (doc.GetElement(r) is CableTray tray) plan.Trays.Add(tray);
                        }
                        plan.NewSelection = true;
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        skipped.Add(route.Key);
                        continue;
                    }
                }

                if (plan.Trays.Count == 0) { skipped.Add(route.Key); continue; }
                plans.Add(plan);
            }

            if (plans.Count == 0)
            {
                message = "Tidak ada jalur yang diproses (semua dilewati).";
                return Result.Cancelled;
            }

            ElementId conduitTypeId = new FilteredElementCollector(doc)
                .OfClass(typeof(ConduitType)).FirstElementId();
            if (conduitTypeId == null || conduitTypeId == ElementId.InvalidElementId)
            {
                message = "Project ini tidak memiliki Conduit Type. " +
                          "Gunakan template Electrical atau load type conduit dahulu.";
                return Result.Failed;
            }

            // ---------- 3. Transaction: hapus conduit lama + gambar ulang ----------
            var summary = new StringBuilder();
            int totalCreated = 0, totalDeleted = 0, totalElbow = 0;

            using (var t = new Transaction(doc, "Pull Cable Tray Hub: " + sim.Id))
            {
                t.Start();

                foreach (var plan in plans)
                {
                    RouteInfo route = plan.Route;

                    // 3a. Sinkron: hapus conduit lama milik jalur ini
                    int deleted = DeleteTaggedElements(doc, route.Key);
                    totalDeleted += deleted;

                    // 3b. Susun jalur geometri dari tray
                    var segments = new List<Line>();
                    foreach (var tray in plan.Trays)
                    {
                        if (tray.Location is LocationCurve lc && lc.Curve is Line line &&
                            line.Length > MinSegmentFt)
                        {
                            segments.Add(line);
                        }
                    }
                    if (segments.Count == 0)
                    {
                        summary.AppendLine($"✖ {route.Key}: tray tidak punya sumbu lurus valid.");
                        continue;
                    }

                    List<Line> chain = ChainSegments(segments);
                    bool isChained = chain != null;
                    if (!isChained) chain = segments;

                    // 3c. Gambar conduit per kabel
                    var slots = new List<CableInfo>();
                    foreach (var k in route.Kabel)
                        for (int i = 0; i < k.Qty; i++) slots.Add(k);

                    double maxDeMm = slots.Max(k => k.Diameter);
                    double spacingFt = (maxDeMm + 2) * MmToFt;

                    ElementId levelId = plan.Trays[0].ReferenceLevel?.Id
                        ?? new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElementId();

                    int created = 0, elbows = 0;
                    for (int slot = 0; slot < slots.Count; slot++)
                    {
                        CableInfo cable = slots[slot];
                        double offset = (slot - (slots.Count - 1) / 2.0) * spacingFt;
                        string tag = TagPrefix + route.Key + "|" + cable.Nama;

                        var runConduits = new List<Conduit>();
                        if (isChained)
                        {
                            List<XYZ> pts = BuildOffsetPolyline(chain, offset);
                            for (int i = 0; i < pts.Count - 1; i++)
                            {
                                Conduit c = CreateConduit(doc, conduitTypeId, levelId,
                                    pts[i], pts[i + 1], cable, tag);
                                if (c != null) { runConduits.Add(c); created++; }
                            }
                        }
                        else
                        {
                            foreach (var seg in chain)
                            {
                                XYZ n = PerpendicularOf(seg.Direction);
                                Conduit c = CreateConduit(doc, conduitTypeId, levelId,
                                    seg.GetEndPoint(0) + n * offset,
                                    seg.GetEndPoint(1) + n * offset,
                                    cable, tag);
                                if (c != null) { runConduits.Add(c); created++; }
                            }
                        }

                        string fittingTag = TagPrefix + route.Key + "|fitting";
                        for (int i = 0; i < runConduits.Count - 1; i++)
                        {
                            if (TryCreateElbow(doc, runConduits[i], runConduits[i + 1], fittingTag))
                                elbows++;
                        }
                    }

                    totalCreated += created;
                    totalElbow += elbows;

                    // 3d. Ingat pilihan tray untuk pull berikutnya
                    mapping[route.Key] = plan.Trays.Select(tr => tr.UniqueId).ToList();

                    summary.AppendLine(
                        $"✔ {route.Key}: {created} conduit" +
                        (deleted > 0 ? $" (menggantikan {deleted} lama)" : " (baru)") +
                        (plan.NewSelection ? "" : " — pakai tray tersimpan"));
                }

                SyncStorage.Save(doc, mapping);
                t.Commit();
            }

            // ---------- 4. Laporan ----------
            foreach (var s in skipped) summary.AppendLine($"◌ {s}: dilewati.");

            var td = new TaskDialog("Cable Tray Hub — Pull Selesai")
            {
                MainInstruction = $"Sinkronisasi {sim.Id} selesai",
                MainContent =
                    $"Proyek: {sim.NamaProyek}\n" +
                    $"Conduit dibuat: {totalCreated} | dihapus (versi lama): {totalDeleted} | elbow: {totalElbow}\n\n" +
                    summary
            };
            td.Show();

            return Result.Succeeded;
        }

        // =================================================================
        //  SINKRONISASI: HAPUS ELEMEN BERTANDA JALUR
        // =================================================================

        private static int DeleteTaggedElements(Document doc, string routeKey)
        {
            string prefix = TagPrefix + routeKey + "|";
            var ids = new List<ElementId>();

            var conduits = new FilteredElementCollector(doc).OfClass(typeof(Conduit));
            foreach (Element e in conduits)
            {
                if (GetComments(e).StartsWith(prefix)) ids.Add(e.Id);
            }

            var fittings = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_ConduitFitting)
                .WhereElementIsNotElementType();
            foreach (Element e in fittings)
            {
                if (GetComments(e).StartsWith(prefix)) ids.Add(e.Id);
            }

            int deleted = 0;
            foreach (var id in ids)
            {
                // Menghapus conduit bisa ikut menghapus fitting tetangganya,
                // jadi cek dulu apakah elemen masih ada.
                if (doc.GetElement(id) == null) continue;
                try { doc.Delete(id); deleted++; } catch { /* sudah terhapus */ }
            }
            return deleted;
        }

        internal static string GetComments(Element e)
        {
            Parameter p = e.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            return p != null ? (p.AsString() ?? "") : "";
        }

        // =================================================================
        //  PEMBUATAN CONDUIT
        // =================================================================

        private static Conduit CreateConduit(Document doc, ElementId typeId, ElementId levelId,
            XYZ start, XYZ end, CableInfo cable, string tag)
        {
            if (start.DistanceTo(end) < MinSegmentFt) return null;

            Conduit conduit;
            try
            {
                conduit = Conduit.Create(doc, typeId, start, end, levelId);
            }
            catch
            {
                return null;
            }

            // Diameter nominal sesuai diameter luar kabel (harus ada di Conduit Sizes)
            try
            {
                Parameter dia = conduit.get_Parameter(BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM);
                if (dia != null && !dia.IsReadOnly) dia.Set(cable.Diameter * MmToFt);
            }
            catch { /* ukuran tidak terdaftar -> pakai default type */ }

            // Penanda sinkronisasi (dibaca saat pull berikutnya & saat push)
            try
            {
                Parameter cmt = conduit.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (cmt != null && !cmt.IsReadOnly) cmt.Set(tag);
            }
            catch { /* opsional */ }

            return conduit;
        }

        private static bool TryCreateElbow(Document doc, Conduit a, Conduit b, string tag)
        {
            try
            {
                Connector ca = NearestFreeConnector(a, b);
                Connector cb = NearestFreeConnector(b, a);
                if (ca == null || cb == null) return false;
                FamilyInstance elbow = doc.Create.NewElbowFitting(ca, cb);
                try
                {
                    Parameter cmt = elbow.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    if (cmt != null && !cmt.IsReadOnly) cmt.Set(tag);
                }
                catch { }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Connector NearestFreeConnector(Conduit from, Conduit toward)
        {
            var target = (toward.Location as LocationCurve).Curve.Evaluate(0.5, true);
            Connector best = null;
            double bestDist = double.MaxValue;
            foreach (Connector c in from.ConnectorManager.Connectors)
            {
                if (c.IsConnected) continue;
                double d = c.Origin.DistanceTo(target);
                if (d < bestDist) { bestDist = d; best = c; }
            }
            return best;
        }

        // =================================================================
        //  GEOMETRI JALUR
        // =================================================================

        /// <summary>
        /// Mengurutkan segmen tray menjadi rantai menerus Panel A -> Panel B.
        /// Arah tiap segmen dibalik bila perlu agar alirannya konsisten.
        /// Mengembalikan null bila segmen tidak membentuk satu jalur.
        /// </summary>
        private static List<Line> ChainSegments(List<Line> segments)
        {
            if (segments.Count == 1) return new List<Line>(segments);

            var remaining = new List<Line>(segments);
            var chain = new LinkedList<Line>();
            chain.AddFirst(remaining[0]);
            remaining.RemoveAt(0);

            while (remaining.Count > 0)
            {
                XYZ tail = chain.Last.Value.GetEndPoint(1);
                XYZ head = chain.First.Value.GetEndPoint(0);

                int bestIdx = -1;
                bool atTail = true, reversed = false;
                double bestDist = JoinToleranceFt;

                for (int i = 0; i < remaining.Count; i++)
                {
                    XYZ p0 = remaining[i].GetEndPoint(0);
                    XYZ p1 = remaining[i].GetEndPoint(1);

                    double d;
                    d = tail.DistanceTo(p0); if (d < bestDist) { bestDist = d; bestIdx = i; atTail = true; reversed = false; }
                    d = tail.DistanceTo(p1); if (d < bestDist) { bestDist = d; bestIdx = i; atTail = true; reversed = true; }
                    d = head.DistanceTo(p1); if (d < bestDist) { bestDist = d; bestIdx = i; atTail = false; reversed = false; }
                    d = head.DistanceTo(p0); if (d < bestDist) { bestDist = d; bestIdx = i; atTail = false; reversed = true; }
                }

                if (bestIdx < 0) return null; // ada segmen yang terputus dari jalur

                Line next = remaining[bestIdx];
                remaining.RemoveAt(bestIdx);
                if (reversed) next = Line.CreateBound(next.GetEndPoint(1), next.GetEndPoint(0));

                if (atTail) chain.AddLast(next);
                else chain.AddFirst(next);
            }

            return chain.ToList();
        }

        /// <summary>
        /// Vektor tegak lurus yang konsisten terhadap arah segmen
        /// (untuk menggeser conduit ke samping tray).
        /// </summary>
        private static XYZ PerpendicularOf(XYZ direction)
        {
            XYZ n = XYZ.BasisZ.CrossProduct(direction);
            if (n.GetLength() < 1e-6) n = direction.CrossProduct(XYZ.BasisX); // segmen vertikal
            if (n.GetLength() < 1e-6) n = XYZ.BasisY;
            return n.Normalize();
        }

        /// <summary>
        /// Membangun polyline offset sejauh <paramref name="offset"/> dari rantai
        /// segmen. Titik belokan dihitung dari perpotongan dua garis offset
        /// (miter corner) agar conduit paralel tetap rapi di tikungan.
        /// </summary>
        private static List<XYZ> BuildOffsetPolyline(List<Line> chain, double offset)
        {
            var points = new List<XYZ>();

            XYZ n0 = PerpendicularOf(chain[0].Direction);
            points.Add(chain[0].GetEndPoint(0) + n0 * offset);

            for (int i = 0; i < chain.Count - 1; i++)
            {
                XYZ na = PerpendicularOf(chain[i].Direction);
                XYZ nb = PerpendicularOf(chain[i + 1].Direction);

                XYZ a1 = chain[i].GetEndPoint(0) + na * offset;
                XYZ a2 = chain[i].GetEndPoint(1) + na * offset;
                XYZ b1 = chain[i + 1].GetEndPoint(0) + nb * offset;
                XYZ b2 = chain[i + 1].GetEndPoint(1) + nb * offset;

                XYZ corner = IntersectLines(a1, a2 - a1, b1, b2 - b1)
                             ?? (a2 + b1) / 2.0; // segmen paralel/sejajar -> titik tengah
                points.Add(corner);
            }

            XYZ nLast = PerpendicularOf(chain[^1].Direction);
            points.Add(chain[^1].GetEndPoint(1) + nLast * offset);

            return points;
        }

        /// <summary>
        /// Titik potong (closest point) dua garis 3D p = p1 + t*d1 dan q = p2 + s*d2.
        /// Mengembalikan null bila hampir paralel.
        /// </summary>
        private static XYZ IntersectLines(XYZ p1, XYZ d1, XYZ p2, XYZ d2)
        {
            double a = d1.DotProduct(d1);
            double b = d1.DotProduct(d2);
            double c = d2.DotProduct(d2);
            double denom = a * c - b * b;
            if (Math.Abs(denom) < 1e-9) return null;

            XYZ w = p1 - p2;
            double dCoef = d1.DotProduct(w);
            double e = d2.DotProduct(w);

            double t = (b * e - c * dCoef) / denom;
            double s = (a * e - b * dCoef) / denom;

            XYZ onFirst = p1 + d1 * t;
            XYZ onSecond = p2 + d2 * s;
            return (onFirst + onSecond) / 2.0;
        }
    }

    /// <summary>Filter selection: hanya cable tray yang bisa dipilih.</summary>
    public class CableTrayFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is CableTray;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
