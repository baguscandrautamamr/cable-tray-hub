using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Windows.Forms;
// Revit UI dan WinForms sama-sama punya class TaskDialog — pakai versi Revit
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace CableTrayHub.Revit
{
    /// <summary>
    /// Alur utama:
    /// 1. Dialog -> ambil simulasi dari website (Google Sheets API).
    /// 2. User memilih segmen-segmen cable tray dari Panel A ke Panel B.
    /// 3. Segmen dirantai menjadi satu jalur menerus.
    /// 4. Setiap kabel pada simulasi digambar sebagai conduit paralel
    ///    mengikuti jalur tray (lengkap dengan elbow di belokan).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ImportSimulationCommand : IExternalCommand
    {
        private const double MmToFt = 1.0 / 304.8;
        // Dua segmen tray dianggap tersambung bila ujungnya berjarak < 60 cm
        // (memberi toleransi untuk fitting/elbow di antara segmen).
        private const double JoinToleranceFt = 600 * MmToFt;
        private const double MinSegmentFt = 50 * MmToFt;

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

            // ---------- 2. User memilih cable tray ----------
            IList<Reference> pickedRefs;
            try
            {
                pickedRefs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new CableTrayFilter(),
                    "Pilih segmen cable tray dari Panel A ke Panel B, lalu klik Finish");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            var trays = new List<CableTray>();
            foreach (var r in pickedRefs)
            {
                if (doc.GetElement(r) is CableTray tray) trays.Add(tray);
            }
            if (trays.Count == 0)
            {
                message = "Tidak ada cable tray yang dipilih.";
                return Result.Failed;
            }

            var segments = new List<Line>();
            foreach (var tray in trays)
            {
                if (tray.Location is LocationCurve lc && lc.Curve is Line line &&
                    line.Length > MinSegmentFt)
                {
                    segments.Add(line);
                }
            }
            if (segments.Count == 0)
            {
                message = "Cable tray terpilih tidak memiliki garis sumbu lurus yang valid.";
                return Result.Failed;
            }

            // ---------- 3. Rantai segmen menjadi jalur menerus ----------
            List<Line> chain = ChainSegments(segments);
            bool isChained = chain != null;
            if (!isChained) chain = segments; // fallback: gambar lurus per segmen tanpa elbow

            // ---------- 4. Susun daftar conduit (1 kabel = 1 conduit) ----------
            var slots = new List<CableInfo>();
            foreach (var k in sim.Detail.Kabel)
                for (int i = 0; i < k.Qty; i++) slots.Add(k);

            double maxDeMm = slots.Max(k => k.Diameter);
            double spacingMm = sim.Detail.Metode == "Flat Spaced" ? maxDeMm + 10 : maxDeMm + 2;
            double spacingFt = spacingMm * MmToFt;

            ElementId conduitTypeId = new FilteredElementCollector(doc)
                .OfClass(typeof(ConduitType)).FirstElementId();
            if (conduitTypeId == null || conduitTypeId == ElementId.InvalidElementId)
            {
                message = "Project ini tidak memiliki Conduit Type. " +
                          "Load family/type conduit terlebih dahulu (template Electrical).";
                return Result.Failed;
            }

            ElementId levelId = trays[0].ReferenceLevel?.Id
                ?? new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElementId();

            int createdCount = 0, elbowCount = 0, diameterFail = 0;

            using (var t = new Transaction(doc, "Import Kabel: " + sim.Id))
            {
                t.Start();

                for (int slot = 0; slot < slots.Count; slot++)
                {
                    CableInfo cable = slots[slot];
                    double offset = (slot - (slots.Count - 1) / 2.0) * spacingFt;

                    List<XYZ> pathPoints = isChained
                        ? BuildOffsetPolyline(chain, offset)
                        : null;

                    var conduitsInRun = new List<Conduit>();

                    if (pathPoints != null)
                    {
                        for (int i = 0; i < pathPoints.Count - 1; i++)
                        {
                            Conduit c = CreateConduit(doc, conduitTypeId, levelId,
                                pathPoints[i], pathPoints[i + 1], cable, sim, ref diameterFail);
                            if (c != null) { conduitsInRun.Add(c); createdCount++; }
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
                                cable, sim, ref diameterFail);
                            if (c != null) { conduitsInRun.Add(c); createdCount++; }
                        }
                    }

                    // Sambungkan belokan dengan elbow fitting
                    for (int i = 0; i < conduitsInRun.Count - 1; i++)
                    {
                        if (TryCreateElbow(doc, conduitsInRun[i], conduitsInRun[i + 1]))
                            elbowCount++;
                    }
                }

                t.Commit();
            }

            // ---------- 5. Laporan hasil ----------
            var td = new TaskDialog("Cable Tray Hub")
            {
                MainInstruction = "Import simulasi " + sim.Id + " selesai",
                MainContent =
                    $"Proyek: {sim.NamaProyek}\n" +
                    $"Conduit dibuat: {createdCount} (untuk {slots.Count} jalur kabel)\n" +
                    $"Elbow terpasang: {elbowCount}\n" +
                    (isChained ? "" : "Catatan: segmen tray tidak menerus — conduit digambar per segmen tanpa elbow.\n") +
                    (diameterFail > 0
                        ? $"Catatan: {diameterFail} conduit memakai diameter default karena ukuran " +
                          "kabel tidak ada di tabel Conduit Sizes."
                        : "")
            };
            td.Show();

            return Result.Succeeded;
        }

        // =================================================================
        //  PEMBUATAN CONDUIT
        // =================================================================

        private static Conduit CreateConduit(Document doc, ElementId typeId, ElementId levelId,
            XYZ start, XYZ end, CableInfo cable, Simulation sim, ref int diameterFail)
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
            catch { diameterFail++; }

            // Jejak data: nama kabel + ID simulasi di parameter Comments
            try
            {
                Parameter cmt = conduit.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (cmt != null && !cmt.IsReadOnly) cmt.Set($"{cable.Nama} | {sim.Id}");
            }
            catch { /* opsional */ }

            return conduit;
        }

        private static bool TryCreateElbow(Document doc, Conduit a, Conduit b)
        {
            try
            {
                Connector ca = NearestFreeConnector(a, b);
                Connector cb = NearestFreeConnector(b, a);
                if (ca == null || cb == null) return false;
                doc.Create.NewElbowFitting(ca, cb);
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
