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
    /// 2. Jalur baru  : user select cable tray (beserta fitting/elbow di antaranya)
    ///    sekali -> conduit digambar, pilihan diingat di dalam file Revit.
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
        // Dua segmen tray dianggap tersambung bila ujungnya berjarak < 60 cm,
        // ATAU keduanya menempel pada fitting (elbow/tee) yang ikut dipilih —
        // fitting menjembatani jarak berapa pun (radius elbow bisa besar).
        private const double JoinToleranceFt = 600 * MmToFt;
        private const double MinSegmentFt = 50 * MmToFt;
        // Dua segmen dengan sudut < ~1° dianggap segaris: tidak perlu elbow.
        private const double CollinearAngleRad = 0.02;

        private class RoutePlan
        {
            public RouteInfo Route;
            public List<CableTray> Trays = new();
            public List<FamilyInstance> Fittings = new();
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
                    bool utuh = true;
                    foreach (string uid in uniqueIds)
                    {
                        Element el = doc.GetElement(uid);
                        if (el is CableTray tray) plan.Trays.Add(tray);
                        else if (el is FamilyInstance fi) plan.Fittings.Add(fi);
                        else { utuh = false; break; } // ada elemen yang hilang -> select ulang
                    }
                    if (!utuh || plan.Trays.Count == 0)
                    {
                        plan.Trays.Clear();
                        plan.Fittings.Clear();
                    }
                }

                if (plan.Trays.Count == 0)
                {
                    TaskDialog.Show("Cable Tray Hub",
                        $"JALUR: {route.Key}\n\nSetelah menutup dialog ini, pilih segmen-segmen " +
                        $"cable tray dari \"{route.PanelFrom}\" ke \"{route.PanelTo}\" — " +
                        "termasuk fitting/elbow di antaranya — lalu klik Finish.\n" +
                        "(Tekan Esc untuk melewati jalur ini.)");
                    try
                    {
                        IList<Reference> refs = uidoc.Selection.PickObjects(
                            ObjectType.Element, new CableTrayFilter(),
                            $"Pilih cable tray + fitting jalur {route.Key}, lalu klik Finish");
                        foreach (var r in refs)
                        {
                            Element el = doc.GetElement(r);
                            if (el is CableTray tray) plan.Trays.Add(tray);
                            else if (el is FamilyInstance fi) plan.Fittings.Add(fi);
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

            // Pilih ConduitType yang punya aturan elbow di Routing Preferences —
            // tipe "without fittings" membuat NewElbowFitting selalu gagal.
            ConduitType conduitType = PickConduitType(doc, out bool typeHasElbow);
            if (conduitType == null)
            {
                message = "Project ini tidak memiliki Conduit Type. " +
                          "Gunakan template Electrical atau load type conduit dahulu.";
                return Result.Failed;
            }

            // ---------- 3. Transaction: hapus conduit lama + gambar ulang ----------
            var summary = new StringBuilder();
            int totalCreated = 0, totalDeleted = 0, totalElbow = 0, totalBend = 0;

            using (var t = new Transaction(doc, "Pull Cable Tray Hub: " + sim.Id))
            {
                t.Start();

                foreach (var plan in plans)
                {
                    RouteInfo route = plan.Route;

                    // 3a. Sinkron: hapus conduit lama milik jalur ini
                    int deleted = DeleteTaggedElements(doc, route.Key);
                    totalDeleted += deleted;

                    // 3b. Susun jalur geometri dari tray (fitting = jembatan sambungan)
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

                    List<List<XYZ>> bridges = FittingNodes(plan.Fittings);
                    List<Line> chain = ChainSegments(segments, bridges);
                    bool isChained = chain != null;
                    if (!isChained) chain = segments;

                    // 3c. Gambar conduit per kabel — mengikuti POSISI PENAMPANG
                    // dari kanvas visual website (x dari dinding kiri tray,
                    // y dari dasar tray). Jika data posisi tidak ada (simulasi
                    // lama), fallback ke barisan rata di tengah tray.
                    var slots = new List<(CableInfo Cable, PosXY Pos)>();
                    foreach (var k in route.Kabel)
                        for (int i = 0; i < k.Qty; i++)
                            slots.Add((k, k.Posisi != null && i < k.Posisi.Count ? k.Posisi[i] : null));

                    double trayWmm = sim.Detail?.Tray?.Lebar > 0 ? sim.Detail.Tray.Lebar : 300;
                    double trayHmm = sim.Detail?.Tray?.Tinggi > 0 ? sim.Detail.Tray.Tinggi : 100;
                    double maxDeMm = slots.Max(s => s.Cable.Diameter);
                    double spacingFt = (maxDeMm + 2) * MmToFt;

                    ElementId levelId = plan.Trays[0].ReferenceLevel?.Id
                        ?? new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElementId();

                    // Frame penampang tiap segmen: dihitung SEKALI untuk seluruh
                    // rantai dengan parallel transport (frame ikut berputar di
                    // belokan), sehingga susunan kabel kontinu — termasuk saat
                    // jalur turun vertikal ke panel.
                    List<(XYZ N, XYZ V)> frames = isChained ? BuildFrames(chain) : null;

                    int created = 0, elbows = 0, bends = 0;
                    for (int slot = 0; slot < slots.Count; slot++)
                    {
                        CableInfo cable = slots[slot].Cable;
                        PosXY pos = slots[slot].Pos;

                        // Offset penampang relatif sumbu tray (sumbu = tengah lebar & tinggi)
                        double lat, vert;
                        if (pos != null)
                        {
                            lat = (pos.X - trayWmm / 2.0) * MmToFt;
                            vert = (pos.Y - trayHmm / 2.0) * MmToFt;
                        }
                        else
                        {
                            lat = (slot - (slots.Count - 1) / 2.0) * spacingFt;
                            vert = (cable.Diameter / 2.0 - trayHmm / 2.0) * MmToFt; // duduk di dasar tray
                        }

                        string tag = TagPrefix + route.Key + "|" + cable.Nama;

                        var runConduits = new List<Conduit>();
                        if (isChained)
                        {
                            List<XYZ> pts = BuildOffsetPolyline(chain, frames, lat, vert);
                            for (int i = 0; i < pts.Count - 1; i++)
                            {
                                Conduit c = CreateConduit(doc, conduitType.Id, levelId,
                                    pts[i], pts[i + 1], cable, tag);
                                if (c != null) { runConduits.Add(c); created++; }
                            }
                        }
                        else
                        {
                            foreach (var seg in chain)
                            {
                                XYZ off = OffsetVector(seg.Direction, lat, vert);
                                Conduit c = CreateConduit(doc, conduitType.Id, levelId,
                                    seg.GetEndPoint(0) + off,
                                    seg.GetEndPoint(1) + off,
                                    cable, tag);
                                if (c != null) { runConduits.Add(c); created++; }
                            }
                        }

                        string fittingTag = TagPrefix + route.Key + "|fitting";
                        for (int i = 0; i < runConduits.Count - 1; i++)
                        {
                            bends++;
                            if (TryCreateElbow(doc, runConduits[i], runConduits[i + 1], fittingTag))
                                elbows++;
                        }
                    }

                    totalCreated += created;
                    totalElbow += elbows;
                    totalBend += bends;

                    // 3d. Ingat pilihan tray + fitting untuk pull berikutnya
                    var remembered = plan.Trays.Select(tr => tr.UniqueId).ToList();
                    remembered.AddRange(plan.Fittings.Select(f => f.UniqueId));
                    mapping[route.Key] = remembered;

                    summary.AppendLine(
                        $"✔ {route.Key}: {created} conduit, {elbows} elbow" +
                        (isChained ? "" : " — segmen tidak menyambung, digambar per segmen") +
                        (deleted > 0 ? $" (menggantikan {deleted} lama)" : " (baru)") +
                        (plan.NewSelection ? "" : " — pakai tray tersimpan"));
                }

                SyncStorage.Save(doc, mapping);
                t.Commit();
            }

            // ---------- 4. Laporan ----------
            foreach (var s in skipped) summary.AppendLine($"◌ {s}: dilewati.");

            if (!typeHasElbow && totalBend > 0)
                summary.AppendLine(
                    "\n⚠ Tipe conduit di project ini tidak punya fitting Elbow di Routing " +
                    "Preferences, jadi elbow tidak bisa dibuat otomatis. Gunakan tipe " +
                    "\"Conduit with Fittings\" atau isi Routing Preferences-nya.");

            var td = new TaskDialog("Cable Tray Hub — Pull Selesai")
            {
                MainInstruction = $"Sinkronisasi {sim.Id} selesai",
                MainContent =
                    $"Proyek: {sim.NamaProyek}\n" +
                    $"Conduit dibuat: {totalCreated} | dihapus (versi lama): {totalDeleted} | " +
                    $"elbow: {totalElbow}/{totalBend}\n\n" +
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

        /// <summary>
        /// Pilih ConduitType yang Routing Preferences-nya punya aturan Elbow
        /// (mis. "Conduit with Fittings"); kalau tidak ada, pakai tipe pertama.
        /// </summary>
        private static ConduitType PickConduitType(Document doc, out bool hasElbow)
        {
            ConduitType first = null;
            foreach (ConduitType ct in new FilteredElementCollector(doc)
                         .OfClass(typeof(ConduitType)).Cast<ConduitType>())
            {
                first ??= ct;
                if (HasElbowRule(ct)) { hasElbow = true; return ct; }
            }
            hasElbow = false;
            return first;
        }

        private static bool HasElbowRule(ConduitType ct)
        {
            try
            {
                return ct.RoutingPreferenceManager?
                    .GetNumberOfRules(RoutingPreferenceRuleGroupType.Elbows) > 0;
            }
            catch { return false; }
        }

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
                // Pasangan connector bebas yang saling terdekat antara kedua conduit
                Connector bestA = null, bestB = null;
                double bestDist = double.MaxValue;
                foreach (Connector ca in a.ConnectorManager.Connectors)
                {
                    if (ca.IsConnected) continue;
                    foreach (Connector cb in b.ConnectorManager.Connectors)
                    {
                        if (cb.IsConnected) continue;
                        double d = ca.Origin.DistanceTo(cb.Origin);
                        if (d < bestDist) { bestDist = d; bestA = ca; bestB = cb; }
                    }
                }
                if (bestA == null || bestB == null) return false;

                FamilyInstance elbow = doc.Create.NewElbowFitting(bestA, bestB);
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

        // =================================================================
        //  GEOMETRI JALUR
        // =================================================================

        /// <summary>
        /// Titik-titik connector tiap fitting yang dipilih user — dipakai
        /// sebagai jembatan saat merantai segmen tray.
        /// </summary>
        private static List<List<XYZ>> FittingNodes(List<FamilyInstance> fittings)
        {
            var nodes = new List<List<XYZ>>();
            foreach (var fi in fittings)
            {
                var pts = new List<XYZ>();
                try
                {
                    var cm = fi.MEPModel?.ConnectorManager;
                    if (cm != null)
                        foreach (Connector c in cm.Connectors) pts.Add(c.Origin);
                }
                catch { }
                if (pts.Count == 0 && fi.Location is LocationPoint lp) pts.Add(lp.Point);
                if (pts.Count > 0) nodes.Add(pts);
            }
            return nodes;
        }

        /// <summary>
        /// Mengurutkan segmen tray menjadi rantai menerus Panel A -> Panel B.
        /// Arah tiap segmen dibalik bila perlu agar alirannya konsisten.
        /// Dua ujung dianggap tersambung bila berdekatan langsung, atau bila
        /// keduanya menempel pada fitting yang sama (elbow/tee ikut dipilih).
        /// Mengembalikan null bila segmen tidak membentuk satu jalur.
        /// </summary>
        private static List<Line> ChainSegments(List<Line> segments, List<List<XYZ>> bridges)
        {
            if (segments.Count == 1) return new List<Line>(segments);

            double Dist(XYZ e, XYZ p)
            {
                double d = e.DistanceTo(p);
                foreach (var pts in bridges)
                {
                    double da = double.MaxValue, db = double.MaxValue;
                    foreach (var q in pts)
                    {
                        da = Math.Min(da, e.DistanceTo(q));
                        db = Math.Min(db, p.DistanceTo(q));
                    }
                    if (da < JoinToleranceFt && db < JoinToleranceFt)
                        d = Math.Min(d, Math.Max(da, db));
                }
                return d;
            }

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
                    d = Dist(tail, p0); if (d < bestDist) { bestDist = d; bestIdx = i; atTail = true; reversed = false; }
                    d = Dist(tail, p1); if (d < bestDist) { bestDist = d; bestIdx = i; atTail = true; reversed = true; }
                    d = Dist(head, p1); if (d < bestDist) { bestDist = d; bestIdx = i; atTail = false; reversed = false; }
                    d = Dist(head, p0); if (d < bestDist) { bestDist = d; bestIdx = i; atTail = false; reversed = true; }
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
        /// Vektor offset penampang untuk SATU segmen lepas (fallback saat rantai
        /// tidak tersambung). Untuk rantai menerus, gunakan BuildFrames yang
        /// menjaga orientasi kontinu di belokan.
        /// </summary>
        private static XYZ OffsetVector(XYZ direction, double lat, double vert)
        {
            XYZ n = PerpendicularOf(direction);
            XYZ v = direction.CrossProduct(n);
            if (v.GetLength() < 1e-6) v = XYZ.BasisZ;
            v = v.Normalize();
            if (v.Z < 0) v = v.Negate(); // pastikan "atas penampang" mengarah ke atas
            return n * lat + v * vert;
        }

        /// <summary>
        /// Frame penampang (N = lateral, V = vertikal penampang) untuk tiap
        /// segmen rantai, dihitung dengan PARALLEL TRANSPORT: frame segmen
        /// pertama diputar mengikuti setiap belokan (sumbu putar = d1 × d2).
        /// Dengan ini susunan kabel tidak "melompat" saat jalur berbelok,
        /// termasuk belokan horizontal → vertikal (turun ke panel).
        /// </summary>
        private static List<(XYZ N, XYZ V)> BuildFrames(List<Line> chain)
        {
            var frames = new List<(XYZ N, XYZ V)>();

            XYZ d0 = chain[0].Direction;
            XYZ n = PerpendicularOf(d0);
            XYZ v = d0.CrossProduct(n);
            if (v.GetLength() < 1e-6) v = XYZ.BasisZ;
            v = v.Normalize();
            if (v.Z < 0) v = v.Negate(); // konvensi sama dengan OffsetVector
            frames.Add((n, v));

            for (int i = 1; i < chain.Count; i++)
            {
                XYZ dPrev = chain[i - 1].Direction;
                XYZ dNext = chain[i].Direction;
                XYZ axis = dPrev.CrossProduct(dNext);
                if (axis.GetLength() > 1e-9)
                {
                    Transform rot = Transform.CreateRotation(axis.Normalize(), dPrev.AngleTo(dNext));
                    n = rot.OfVector(n);
                    v = rot.OfVector(v);
                }
                // dPrev sejajar dNext (lurus / balik arah): frame dipertahankan
                frames.Add((n, v));
            }

            return frames;
        }

        /// <summary>
        /// Membangun polyline offset (lateral + vertikal penampang) dari rantai
        /// segmen memakai frame hasil parallel transport. Titik belokan dihitung
        /// dari perpotongan dua garis offset (miter corner) agar conduit paralel
        /// tetap rapi di tikungan; belokan segaris dilewati (jadi satu conduit).
        /// </summary>
        private static List<XYZ> BuildOffsetPolyline(
            List<Line> chain, List<(XYZ N, XYZ V)> frames, double lat, double vert)
        {
            XYZ Off(int i) => frames[i].N * lat + frames[i].V * vert;

            var points = new List<XYZ> { chain[0].GetEndPoint(0) + Off(0) };

            for (int i = 0; i < chain.Count - 1; i++)
            {
                // Segaris: tidak perlu titik belok — biarkan menyatu jadi satu conduit
                if (chain[i].Direction.AngleTo(chain[i + 1].Direction) < CollinearAngleRad)
                    continue;

                XYZ offA = Off(i);
                XYZ offB = Off(i + 1);

                XYZ a1 = chain[i].GetEndPoint(0) + offA;
                XYZ a2 = chain[i].GetEndPoint(1) + offA;
                XYZ b1 = chain[i + 1].GetEndPoint(0) + offB;
                XYZ b2 = chain[i + 1].GetEndPoint(1) + offB;

                XYZ corner = IntersectLines(a1, a2 - a1, b1, b2 - b1)
                             ?? (a2 + b1) / 2.0; // hampir paralel -> titik tengah
                points.Add(corner);
            }

            points.Add(chain[^1].GetEndPoint(1) + Off(chain.Count - 1));

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

    /// <summary>
    /// Filter selection: cable tray DAN fitting-nya (elbow, tee, dsb.)
    /// sama-sama bisa dipilih — fitting dipakai sebagai jembatan sambungan.
    /// </summary>
    public class CableTrayFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) =>
            elem is CableTray ||
            (elem is FamilyInstance &&
             elem.Category?.BuiltInCategory == BuiltInCategory.OST_CableTrayFitting);

        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
