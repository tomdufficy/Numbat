using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using SdColor = System.Drawing.Color;

namespace Numbat.Commands.Analysis.NumbatGfa
{
    /// <summary>How a volume's use is decided. Stored per document.</summary>
    public enum TrackingMode
    {
        /// <summary>Use stored on the object (user string); object colour shows the use.</summary>
        ByObject,
        /// <summary>Use = the GFA sublayer the object is on. Moving objects between layers reassigns them.</summary>
        ByLayer,
    }

    public sealed class VolumeIssue
    {
        public Guid Id { get; internal set; }
        public VolumeStatus Status { get; internal set; }
        public double TiltDegrees { get; internal set; }
    }

    /// <summary>GFA totals for one moment in time. All areas are in the display unit (m² or ft²).</summary>
    public sealed class Tally
    {
        public static readonly Tally Empty = new Tally();

        public TrackingMode Mode { get; internal set; }
        public string Unit { get; internal set; } = "m²";

        /// <summary>Set when the model uses units nbGFA does not officially support (imperial).</summary>
        public string UnitWarning { get; internal set; }

        public double Total { get; internal set; }
        public double Selected { get; internal set; }
        public int SelectedCount { get; internal set; }

        /// <summary>By layer mode: volumes on the GFA::Excluded layer (not part of the total).</summary>
        public double ExcludedArea { get; internal set; }
        public int ExcludedCount { get; internal set; }

        public Dictionary<string, double> AreaByUse { get; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> CountByUse { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Assigned volumes with a problem (not counted, or tilted and not ignored).</summary>
        public List<VolumeIssue> Issues { get; } = new List<VolumeIssue>();

        /// <summary>Assigned objects left out of the totals.</summary>
        public int NotCountedCount => Issues.Count(i => i.Status != VolumeStatus.Tilted);

        public double Area(UseCategory c) => AreaByUse.TryGetValue(c.Name, out double a) ? a : 0;
        public int Count(UseCategory c) => CountByUse.TryGetValue(c.Name, out int n) ? n : 0;
        public double Share(UseCategory c) => Total > 0 ? Area(c) / Total : 0;

        /// <summary>Text describing a broken limit, or null when the use is within its limits.</summary>
        public string Violation(UseCategory c)
        {
            if (Total <= 0 || !c.HasLimits) return null;
            const double eps = 1e-6;
            double share = Share(c);
            if (c.MaxShare.HasValue && share > c.MaxShare.Value + eps) return $"over max {Pct(c.MaxShare.Value)}";
            if (c.MinShare.HasValue && share < c.MinShare.Value - eps) return $"under min {Pct(c.MinShare.Value)}";
            double area = Area(c);
            if (c.MaxArea.HasValue && area > c.MaxArea.Value + eps) return $"over max {AreaText(c.MaxArea.Value)}";
            if (c.MinArea.HasValue && area < c.MinArea.Value - eps) return $"under min {AreaText(c.MinArea.Value)}";
            return null;
        }

        /// <summary>Minimum and maximum total GFA for the document, in the display unit. Null = not set.</summary>
        public double? TargetMin { get; internal set; }
        public double? TargetMax { get; internal set; }
        public bool HasTarget => TargetMin.HasValue || TargetMax.HasValue;

        /// <summary>How far the total is outside its target ("1,200 m² over the max of 10,000 m²"), or null.</summary>
        public string TargetMiss()
        {
            const double eps = 1e-6;
            if (TargetMax.HasValue && Total > TargetMax.Value + eps)
                return $"{AreaText(Total - TargetMax.Value)} over the max of {AreaText(TargetMax.Value)}";
            if (TargetMin.HasValue && Total < TargetMin.Value - eps)
                return $"{AreaText(TargetMin.Value - Total)} under the min of {AreaText(TargetMin.Value)}";
            return null;
        }

        /// <summary>The target as text ("8,000–10,000 m²", "max 10,000 m²"), or null.</summary>
        public string TargetText()
        {
            if (TargetMin.HasValue && TargetMax.HasValue) return $"{Num(TargetMin.Value)}–{AreaText(TargetMax.Value)}";
            if (TargetMax.HasValue) return $"max {AreaText(TargetMax.Value)}";
            if (TargetMin.HasValue) return $"min {AreaText(TargetMin.Value)}";
            return null;
        }

        public static string Pct(double fraction) => $"{fraction * 100:0.#}%";

        public static string Num(double area) => area.ToString("#,0");

        public string AreaText(double area) => $"{Num(area)} {Unit}";
    }

    /// <summary>Full check including the slower overlap test. Built on demand by <see cref="GfaModel.Check"/>.</summary>
    public sealed class CheckReport
    {
        public List<Guid> Tilted { get; } = new List<Guid>();
        public List<Guid> NotClosed { get; } = new List<Guid>();
        public List<Guid> NoFlatBottom { get; } = new List<Guid>();
        public List<Guid> NotSupported { get; } = new List<Guid>();
        public List<(Guid a, Guid b)> Overlaps { get; } = new List<(Guid, Guid)>();

        public bool IsClean => Tilted.Count + NotClosed.Count + NoFlatBottom.Count + NotSupported.Count + Overlaps.Count == 0;
    }

    /// <summary>
    /// Watches the active Rhino document and keeps <see cref="Current"/> up to date.
    /// Document events only mark the tally dirty; the work happens once on the next idle.
    /// </summary>
    public sealed class GfaModel
    {
        /// <summary>Object user string set by "Ignore" on a tilted volume.</summary>
        public const string IgnoreKey = GfaInfo.IgnoreKey;
        private const string IgnoreTiltValue = "tilt";
        private const string ModeEntry = "mode";
        private const string TargetMinEntry = "target.min";
        private const string TargetMaxEntry = "target.max";

        private readonly Dictionary<Guid, (uint serial, VolumeInfo info)> _cache =
            new Dictionary<Guid, (uint, VolumeInfo)>();
        private List<UseCategory> _categories = UseCategory.Defaults();
        private uint _loadedDocSerial;
        private uint _warnedDocSerial;
        private UnitSystem _warnedUnits;
        private bool _dirty = true;

        /// <summary>Raised on the UI thread after the tally or categories change.</summary>
        public event EventHandler Changed;

        public IReadOnlyList<UseCategory> Categories => _categories;
        public TrackingMode Mode { get; private set; } = TrackingMode.ByObject;

        /// <summary>The document's minimum / maximum total GFA in the display unit. Null = not set.</summary>
        public double? TargetMin { get; private set; }
        public double? TargetMax { get; private set; }

        /// <summary>Sets the document's total GFA target (null clears that bound) and refreshes the window.</summary>
        public void SetTarget(RhinoDoc doc, double? min, double? max)
        {
            if (doc == null) return;
            LoadDocumentSettings(doc);
            TargetMin = min;
            TargetMax = max;
            WriteNumber(doc, TargetMinEntry, min);
            WriteNumber(doc, TargetMaxEntry, max);
            _dirty = true;
        }

        private static void WriteNumber(RhinoDoc doc, string entry, double? value)
        {
            if (value.HasValue)
                doc.Strings.SetString(GfaInfo.DocSection, entry, value.Value.ToString("R", CultureInfo.InvariantCulture));
            else
                doc.Strings.Delete(GfaInfo.DocSection, entry);
        }

        private static double? ReadNumber(RhinoDoc doc, string entry)
        {
            string text = doc.Strings.GetValue(GfaInfo.DocSection, entry);
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : (double?)null;
        }
        public Tally Current { get; private set; } = Tally.Empty;

        public void Attach()
        {
            RhinoDoc.AddRhinoObject += OnObjectsChanged;
            RhinoDoc.DeleteRhinoObject += OnObjectsChanged;
            RhinoDoc.UndeleteRhinoObject += OnObjectsChanged;
            RhinoDoc.ReplaceRhinoObject += OnObjectsChanged;
            RhinoDoc.ModifyObjectAttributes += OnObjectsChanged;
            RhinoDoc.SelectObjects += OnObjectsChanged;
            RhinoDoc.DeselectObjects += OnObjectsChanged;
            RhinoDoc.DeselectAllObjects += OnObjectsChanged;
            RhinoDoc.LayerTableEvent += OnObjectsChanged;
            RhinoDoc.EndOpenDocument += OnDocumentChanged;
            RhinoDoc.NewDocument += OnDocumentChanged;
            RhinoDoc.CloseDocument += OnDocumentChanged;
            RhinoDoc.ActiveDocumentChanged += OnDocumentChanged;
            RhinoDoc.DocumentPropertiesChanged += OnObjectsChanged; // units
            RhinoApp.Idle += OnIdle;
        }

        public void Detach()
        {
            RhinoDoc.AddRhinoObject -= OnObjectsChanged;
            RhinoDoc.DeleteRhinoObject -= OnObjectsChanged;
            RhinoDoc.UndeleteRhinoObject -= OnObjectsChanged;
            RhinoDoc.ReplaceRhinoObject -= OnObjectsChanged;
            RhinoDoc.ModifyObjectAttributes -= OnObjectsChanged;
            RhinoDoc.SelectObjects -= OnObjectsChanged;
            RhinoDoc.DeselectObjects -= OnObjectsChanged;
            RhinoDoc.DeselectAllObjects -= OnObjectsChanged;
            RhinoDoc.LayerTableEvent -= OnObjectsChanged;
            RhinoDoc.EndOpenDocument -= OnDocumentChanged;
            RhinoDoc.NewDocument -= OnDocumentChanged;
            RhinoDoc.CloseDocument -= OnDocumentChanged;
            RhinoDoc.ActiveDocumentChanged -= OnDocumentChanged;
            RhinoDoc.DocumentPropertiesChanged -= OnObjectsChanged;
            RhinoApp.Idle -= OnIdle;
        }

        /// <summary>Recompute now instead of waiting for idle (e.g. when the window first opens).</summary>
        public void Refresh()
        {
            _dirty = false;
            Recompute();
        }

        public UseCategory Find(string name) =>
            _categories.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

        // ================================================================ use of an object

        /// <summary>Works out each object's use for the current mode. Build once, then call <see cref="UseOf"/>.</summary>
        private sealed class Resolver
        {
            public TrackingMode Mode;
            public Dictionary<int, UseCategory> LayerMap;
            public int ExcludedLayer = -1;
            public Func<string, UseCategory> FindOrAdd;

            public UseCategory UseOf(RhinoObject obj)
            {
                if (Mode == TrackingMode.ByLayer)
                {
                    return LayerMap.TryGetValue(obj.Attributes.LayerIndex, out UseCategory use) ? use : null;
                }
                string name = obj.Attributes.GetUserString(GfaInfo.UseKey);
                return string.IsNullOrEmpty(name) ? null : FindOrAdd(name);
            }
        }

        private Resolver MakeResolver(RhinoDoc doc)
        {
            LoadDocumentSettings(doc);
            var r = new Resolver { Mode = Mode, FindOrAdd = FindOrAdd };
            if (Mode == TrackingMode.ByLayer)
            {
                Layer root = LayerSync.FindRoot(doc);
                r.LayerMap = LayerSync.BuildMap(doc, root, _categories);
                r.ExcludedLayer = LayerSync.FindExcluded(doc);
            }
            return r;
        }

        private UseCategory FindOrAdd(string name)
        {
            UseCategory use = Find(name);
            if (use != null) return use;
            // A use typed in by hand or from another file: show it rather than lose its area.
            use = new UseCategory(UseCategory.Clean(name), UseCategory.FallbackColor(name));
            _categories.Add(use);
            return use;
        }

        public List<RhinoObject> ObjectsWithUse(RhinoDoc doc, UseCategory use)
        {
            Resolver r = MakeResolver(doc);
            return AllObjects(doc).Where(o => r.UseOf(o) == use).ToList();
        }

        /// <summary>The objects from <paramref name="objects"/> that currently have a use.</summary>
        public List<RhinoObject> WithAnyUse(RhinoDoc doc, IEnumerable<RhinoObject> objects)
        {
            Resolver r = MakeResolver(doc);
            return objects.Where(o => r.UseOf(o) != null).ToList();
        }

        // ================================================================ assignment

        /// <summary>Gives volumes a use. Non-volumes are skipped. Returns how many changed.</summary>
        public int Assign(RhinoDoc doc, IEnumerable<RhinoObject> objects, UseCategory use)
        {
            LoadDocumentSettings(doc);
            int layer = Mode == TrackingMode.ByLayer ? LayerSync.EnsureUseLayer(doc, use) : -1;
            int n = ModifyEach(doc, objects.Where(Volumes.IsCandidate), $"{GfaInfo.Name}: assign {use.Name}", (o, attr) =>
            {
                attr.SetUserString(GfaInfo.UseKey, use.Name);
                if (layer >= 0)
                {
                    attr.LayerIndex = layer;
                    attr.ColorSource = ObjectColorSource.ColorFromLayer;
                }
                else
                {
                    attr.ObjectColor = use.Color;
                    attr.ColorSource = ObjectColorSource.ColorFromObject;
                }
            });
            if (layer >= 0) SaveCategories(doc); // LayerId may be new
            return n;
        }

        /// <summary>Excludes volumes from the total. By object: back to layer colour. By layer: moved to GFA::Excluded.</summary>
        public int Exclude(RhinoDoc doc, IEnumerable<RhinoObject> objects)
        {
            LoadDocumentSettings(doc);
            int layer = Mode == TrackingMode.ByLayer ? LayerSync.EnsureExcluded(doc) : -1;
            return ModifyEach(doc, objects.Where(Volumes.IsCandidate), $"{GfaInfo.Name}: exclude", (o, attr) =>
            {
                attr.DeleteUserString(GfaInfo.UseKey);
                if (layer >= 0) attr.LayerIndex = layer;
                attr.ColorSource = ObjectColorSource.ColorFromLayer;
            });
        }

        // ================================================================ mode

        /// <summary>
        /// Switches between By object and By layer for this document, carrying assignments across.
        /// By layer: assigned volumes move to their GFA sublayer. By object: volumes keep their layer and get
        /// their use stored on the object instead.
        /// </summary>
        public void SetMode(RhinoDoc doc, TrackingMode mode)
        {
            if (doc == null) return;
            LoadDocumentSettings(doc);
            if (mode == Mode) return;
            Resolver before = MakeResolver(doc);
            List<RhinoObject> objects = AllObjects(doc).Where(Volumes.IsCandidate).ToList();

            uint undo = doc.UndoRecordingIsActive ? 0 : doc.BeginUndoRecord($"{GfaInfo.Name}: track by {(mode == TrackingMode.ByLayer ? "layer" : "object")}");
            try
            {
                if (mode == TrackingMode.ByLayer)
                {
                    LayerSync.EnsureExcluded(doc);
                    foreach (UseCategory use in _categories)
                    {
                        int index = LayerSync.EnsureUseLayer(doc, use);
                        LayerSync.SetLayerColor(doc, doc.Layers[index], use.Color); // the use colour wins
                    }
                    var layerOf = _categories.ToDictionary(u => u, u => doc.Layers.FindId(u.LayerId).Index);
                    foreach (RhinoObject obj in objects)
                    {
                        UseCategory use = before.UseOf(obj);
                        if (use == null) continue;
                        ObjectAttributes attr = obj.Attributes.Duplicate();
                        attr.LayerIndex = layerOf[use];
                        attr.ColorSource = ObjectColorSource.ColorFromLayer;
                        doc.Objects.ModifyAttributes(obj, attr, true);
                    }
                }
                else
                {
                    foreach (RhinoObject obj in objects)
                    {
                        UseCategory use = before.UseOf(obj);
                        bool tagged = !string.IsNullOrEmpty(obj.Attributes.GetUserString(GfaInfo.UseKey));
                        if (use == null && !tagged) continue;
                        ObjectAttributes attr = obj.Attributes.Duplicate();
                        if (use == null)
                        {
                            attr.DeleteUserString(GfaInfo.UseKey);
                        }
                        else
                        {
                            attr.SetUserString(GfaInfo.UseKey, use.Name);
                            attr.ObjectColor = use.Color;
                            attr.ColorSource = ObjectColorSource.ColorFromObject;
                        }
                        doc.Objects.ModifyAttributes(obj, attr, true);
                    }
                }
                Mode = mode;
                doc.Strings.SetString(GfaInfo.DocSection, ModeEntry, mode == TrackingMode.ByLayer ? "layer" : "object");
                SaveCategories(doc);
            }
            finally
            {
                if (undo != 0) doc.EndUndoRecord(undo);
            }
            doc.Views.Redraw();
            _dirty = true;
        }

        // ================================================================ editing uses

        /// <summary>Returns why a name can't be used for a use, or null if it is fine.</summary>
        public string ValidateName(string name, UseCategory except = null)
        {
            name = UseCategory.Clean(name);
            if (name.Length == 0) return "Enter a name.";
            if (!ModelComponent.IsValidComponentName(name)) return "That name can't be used as a layer name.";
            if (string.Equals(name, LayerSync.ExcludedName, StringComparison.OrdinalIgnoreCase))
                return $"\"{LayerSync.ExcludedName}\" is reserved.";
            UseCategory clash = Find(name);
            if (clash != null && clash != except) return $"\"{clash.Name}\" already exists.";
            return null;
        }

        public UseCategory AddCategory(RhinoDoc doc, string name, SdColor color)
        {
            if (ValidateName(name) != null) return null;
            LoadDocumentSettings(doc);
            var use = new UseCategory(UseCategory.Clean(name), color);
            _categories.Add(use);
            if (Mode == TrackingMode.ByLayer) LayerSync.EnsureUseLayer(doc, use);
            SaveCategories(doc);
            return use;
        }

        /// <summary>
        /// The use that removed uses hand their volumes to, so they stay in the total, or null when there is none
        /// (or it is the use being removed).
        /// </summary>
        public UseCategory FallbackFor(UseCategory removing)
        {
            UseCategory fallback = Find(UseCategory.UnassignedName);
            return fallback == removing ? null : fallback;
        }

        /// <summary>
        /// Removes a use. Its volumes move to the Unassigned use if there is one (so they stay in the total),
        /// otherwise they are excluded. Returns how many volumes changed.
        /// </summary>
        public int RemoveCategory(RhinoDoc doc, UseCategory use)
        {
            List<RhinoObject> volumes = ObjectsWithUse(doc, use);
            UseCategory fallback = FallbackFor(use);
            int n = fallback != null ? Assign(doc, volumes, fallback) : Exclude(doc, volumes);
            _categories.Remove(use);
            if (Mode == TrackingMode.ByLayer)
            {
                Layer layer = LayerSync.FindUseLayer(doc, use);
                if (layer != null) doc.Layers.Delete(layer.Index, true); // fails harmlessly if other things live on it
            }
            SaveCategories(doc);
            return n;
        }

        /// <summary>Renames a use (and its layer) and re-tags its volumes.</summary>
        public bool RenameCategory(RhinoDoc doc, UseCategory use, string newName)
        {
            if (ValidateName(newName, use) != null) return false;
            List<RhinoObject> volumes = ObjectsWithUse(doc, use);
            use.Name = UseCategory.Clean(newName);
            Layer layer = LayerSync.FindUseLayer(doc, use);
            if (layer != null) LayerSync.SetLayerName(doc, layer, use.Name);
            ModifyEach(doc, volumes, $"{GfaInfo.Name}: rename {use.Name}",
                (o, attr) => attr.SetUserString(GfaInfo.UseKey, use.Name));
            SaveCategories(doc);
            return true;
        }

        /// <summary>Changes a use's colour (and its layer colour) and recolours its volumes.</summary>
        public void RecolorCategory(RhinoDoc doc, UseCategory use, SdColor color)
        {
            use.Color = color;
            Layer layer = LayerSync.FindUseLayer(doc, use);
            if (layer != null) LayerSync.SetLayerColor(doc, layer, color);
            if (Mode == TrackingMode.ByObject)
            {
                ModifyEach(doc, ObjectsWithUse(doc, use), $"{GfaInfo.Name}: recolour {use.Name}",
                    (o, attr) => attr.ObjectColor = color);
            }
            SaveCategories(doc);
        }

        /// <summary>Saves edited limits/colours into the document and refreshes the window.</summary>
        public void SaveCategories(RhinoDoc doc)
        {
            WriteCategories(doc);
            _dirty = true;
        }

        private void WriteCategories(RhinoDoc doc)
        {
            if (doc == null) return;
            string text = string.Join("\n", _categories.Select(c => c.Serialize()));
            if (doc.Strings.GetValue(GfaInfo.DocSection, GfaInfo.CategoriesEntry) != text)
            {
                doc.Strings.SetString(GfaInfo.DocSection, GfaInfo.CategoriesEntry, text);
            }
        }

        // ================================================================ checks and fixes

        /// <summary>Full check of assigned volumes, including overlaps (slower; run on demand).</summary>
        public CheckReport Check(RhinoDoc doc)
        {
            var report = new CheckReport();
            if (doc == null) return report;

            Resolver r = MakeResolver(doc);
            double tol = doc.ModelAbsoluteTolerance;
            var good = new List<(Guid id, VolumeInfo info, BoundingBox plan)>();

            foreach (RhinoObject obj in AllObjects(doc))
            {
                if (r.UseOf(obj) == null) continue;
                VolumeInfo info = Volumes.Analyze(obj, tol, withOutlines: true);
                switch (info.Status)
                {
                    case VolumeStatus.Tilted:
                        if (obj.Attributes.GetUserString(IgnoreKey) != IgnoreTiltValue) report.Tilted.Add(obj.Id);
                        break;
                    case VolumeStatus.NotClosed: report.NotClosed.Add(obj.Id); break;
                    case VolumeStatus.NoFlatBottom: report.NoFlatBottom.Add(obj.Id); break;
                    case VolumeStatus.NotSupported: report.NotSupported.Add(obj.Id); break;
                }
                if (info.Counted && info.Outlines.Count > 0)
                {
                    var plan = BoundingBox.Empty;
                    foreach (Curve c in info.Outlines) plan.Union(c.GetBoundingBox(true));
                    good.Add((obj.Id, info, plan));
                }
            }

            // Overlaps: two volumes whose heights overlap (not just touch) and whose floor plates intersect in plan.
            double minArea = Math.Pow(0.1 / Volumes.LengthScale(doc, out _), 2); // 0.01 m² in model units
            for (int i = 0; i < good.Count; i++)
            {
                for (int j = i + 1; j < good.Count; j++)
                {
                    var a = good[i];
                    var b = good[j];
                    double zOverlap = Math.Min(a.info.ZRange.Max, b.info.ZRange.Max) - Math.Max(a.info.ZRange.Min, b.info.ZRange.Min);
                    if (zOverlap <= tol * 10) continue;
                    if (a.plan.Max.X <= b.plan.Min.X + tol || b.plan.Max.X <= a.plan.Min.X + tol ||
                        a.plan.Max.Y <= b.plan.Min.Y + tol || b.plan.Max.Y <= a.plan.Min.Y + tol) continue;
                    if (PlanOverlap(a.info.Outlines, b.info.Outlines, tol) > minArea) report.Overlaps.Add((a.id, b.id));
                }
            }
            return report;
        }

        private static double PlanOverlap(List<Curve> a, List<Curve> b, double tol)
        {
            double total = 0;
            foreach (Curve ca in a)
            foreach (Curve cb in b)
            {
                Curve pa = Curve.ProjectToPlane(ca, Plane.WorldXY);
                Curve pb = Curve.ProjectToPlane(cb, Plane.WorldXY);
                if (pa == null || pb == null || !pa.IsClosed || !pb.IsClosed) continue;
                Curve[] shared = Curve.CreateBooleanIntersection(pa, pb, tol);
                if (shared == null) continue;
                foreach (Curve s in shared)
                {
                    using (AreaMassProperties amp = AreaMassProperties.Compute(s))
                    {
                        if (amp != null) total += amp.Area;
                    }
                }
            }
            return total;
        }

        /// <summary>Rotates tilted volumes level about the centre of their bottom face. Returns how many.</summary>
        public int Flatten(RhinoDoc doc, IEnumerable<Guid> ids)
        {
            uint undo = doc.UndoRecordingIsActive ? 0 : doc.BeginUndoRecord($"{GfaInfo.Name}: flatten");
            int n = 0;
            try
            {
                foreach (Guid id in ids.ToList())
                {
                    RhinoObject obj = doc.Objects.FindId(id);
                    if (obj == null) continue;
                    VolumeInfo info = Volumes.Analyze(obj, doc.ModelAbsoluteTolerance);
                    if (info.Status != VolumeStatus.Tilted) continue;
                    if (doc.Objects.Transform(obj, Volumes.FlattenTransform(info), true) != Guid.Empty) n++;
                }
            }
            finally
            {
                if (undo != 0) doc.EndUndoRecord(undo);
            }
            doc.Views.Redraw();
            _dirty = true;
            return n;
        }

        /// <summary>Accepts tilted volumes as they are: still counted, no longer flagged.</summary>
        public int IgnoreTilt(RhinoDoc doc, IEnumerable<Guid> ids) =>
            ModifyEach(doc, ids.Select(doc.Objects.FindId).Where(o => o != null), $"{GfaInfo.Name}: ignore tilt",
                (o, attr) => attr.SetUserString(IgnoreKey, IgnoreTiltValue));

        public void Select(RhinoDoc doc, IEnumerable<Guid> ids)
        {
            doc.Objects.UnselectAll();
            foreach (Guid id in ids) doc.Objects.Select(id, true);
            doc.Views.Redraw();
        }

        // ================================================================ internals

        private int ModifyEach(RhinoDoc doc, IEnumerable<RhinoObject> objects, string undoName,
            Action<RhinoObject, ObjectAttributes> edit)
        {
            if (doc == null) return 0;
            List<RhinoObject> list = objects.ToList();
            uint undo = doc.UndoRecordingIsActive ? 0 : doc.BeginUndoRecord(undoName);
            int changed = 0;
            try
            {
                foreach (RhinoObject obj in list)
                {
                    ObjectAttributes attr = obj.Attributes.Duplicate();
                    edit(obj, attr);
                    if (doc.Objects.ModifyAttributes(obj, attr, true)) changed++;
                }
            }
            finally
            {
                if (undo != 0) doc.EndUndoRecord(undo);
            }
            doc.Views.Redraw();
            _dirty = true;
            return changed;
        }

        private void OnObjectsChanged(object sender, EventArgs e) => _dirty = true;

        private void OnDocumentChanged(object sender, EventArgs e)
        {
            _loadedDocSerial = 0; // reload uses and mode from whichever document is active
            _cache.Clear();
            _dirty = true;
        }

        private void OnIdle(object sender, EventArgs e)
        {
            if (!_dirty) return;
            _dirty = false;
            Recompute();
        }

        private void Recompute()
        {
            RhinoDoc doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                Current = Tally.Empty;
                Changed?.Invoke(this, EventArgs.Empty);
                return;
            }

            LoadDocumentSettings(doc);
            if (Mode == TrackingMode.ByLayer) SyncFromLayers(doc);

            double scale = Volumes.AreaScale(doc, out string unit, out bool imperial);
            double tol = doc.ModelAbsoluteTolerance;
            var tally = new Tally { Unit = unit, Mode = Mode, TargetMin = TargetMin, TargetMax = TargetMax };
            if (imperial) WarnImperial(doc, tally, unit);

            Resolver r = MakeResolver(doc);
            var seen = new HashSet<Guid>();

            foreach (RhinoObject obj in AllObjects(doc))
            {
                UseCategory use = r.UseOf(obj);
                bool selected = obj.IsSelected(false) > 0;
                bool onExcluded = r.ExcludedLayer >= 0 && obj.Attributes.LayerIndex == r.ExcludedLayer;
                if (use == null && !selected && !onExcluded) continue;
                if (use == null && !Volumes.IsCandidate(obj)) continue; // ignore stray curves etc.

                seen.Add(obj.Id);
                VolumeInfo info = Analyze(obj, tol);
                double area = info.Area * scale;

                if (selected && info.Counted)
                {
                    tally.Selected += area;
                    tally.SelectedCount++;
                }

                if (use == null)
                {
                    if (onExcluded && info.Counted)
                    {
                        tally.ExcludedArea += area;
                        tally.ExcludedCount++;
                    }
                    continue;
                }

                bool flagged = !info.Counted ||
                               (info.Status == VolumeStatus.Tilted && obj.Attributes.GetUserString(IgnoreKey) != IgnoreTiltValue);
                if (flagged)
                {
                    tally.Issues.Add(new VolumeIssue { Id = obj.Id, Status = info.Status, TiltDegrees = info.TiltDegrees });
                }
                if (!info.Counted) continue;

                tally.Total += area;
                tally.AreaByUse.TryGetValue(use.Name, out double a);
                tally.AreaByUse[use.Name] = a + area;
                tally.CountByUse.TryGetValue(use.Name, out int n);
                tally.CountByUse[use.Name] = n + 1;
            }

            foreach (Guid id in _cache.Keys.Where(id => !seen.Contains(id)).ToList()) _cache.Remove(id);

            Current = tally;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private VolumeInfo Analyze(RhinoObject obj, double tol)
        {
            if (_cache.TryGetValue(obj.Id, out var hit) && hit.serial == obj.RuntimeSerialNumber) return hit.info;
            VolumeInfo info = Volumes.Analyze(obj, tol);
            _cache[obj.Id] = (obj.RuntimeSerialNumber, info);
            return info;
        }

        private void WarnImperial(RhinoDoc doc, Tally tally, string unit)
        {
            tally.UnitWarning = $"{doc.ModelUnitSystem} not officially supported – showing {unit}";
            if (_warnedDocSerial == doc.RuntimeSerialNumber && _warnedUnits == doc.ModelUnitSystem) return;
            _warnedDocSerial = doc.RuntimeSerialNumber;
            _warnedUnits = doc.ModelUnitSystem;
            RhinoApp.WriteLine($"{GfaInfo.Name}: this model is in {doc.ModelUnitSystem}. Imperial units are " +
                               $"not officially supported (use mm or m); GFA is shown in {unit} as a best effort.");
        }

        /// <summary>
        /// By layer mode: the layer tree is the master. New sublayers become uses, renamed or recoloured layers
        /// update their use, and uses whose layer was deleted are removed.
        /// </summary>
        private void SyncFromLayers(RhinoDoc doc)
        {
            Layer root = LayerSync.FindRoot(doc);
            if (root == null)
            {
                // The whole GFA layer was deleted: rebuild it (empty) so assigning keeps working.
                foreach (UseCategory use in _categories) use.LayerId = Guid.Empty;
                foreach (UseCategory use in _categories) LayerSync.EnsureUseLayer(doc, use);
                LayerSync.EnsureExcluded(doc);
                WriteCategories(doc);
                return;
            }

            bool changed = false;
            var matched = new HashSet<UseCategory>();
            foreach (Layer layer in LayerSync.UseLayers(doc, root))
            {
                UseCategory use = _categories.FirstOrDefault(u => u.LayerId == layer.Id && !matched.Contains(u))
                                  ?? _categories.FirstOrDefault(u => !matched.Contains(u) &&
                                         string.Equals(u.Name, layer.Name, StringComparison.OrdinalIgnoreCase));
                if (use == null)
                {
                    use = new UseCategory(layer.Name, layer.Color) { LayerId = layer.Id };
                    _categories.Add(use);
                    changed = true;
                }
                if (use.LayerId != layer.Id) { use.LayerId = layer.Id; changed = true; }
                if (use.Name != layer.Name) { use.Name = layer.Name; changed = true; }
                if (use.Color.ToArgb() != layer.Color.ToArgb()) { use.Color = layer.Color; changed = true; }
                matched.Add(use);
            }

            // Uses without a live layer were deleted in the Layers panel.
            int removed = _categories.RemoveAll(u => !matched.Contains(u));
            if (changed || removed > 0) WriteCategories(doc);
        }

        private void LoadDocumentSettings(RhinoDoc doc)
        {
            if (_loadedDocSerial == doc.RuntimeSerialNumber) return;
            _loadedDocSerial = doc.RuntimeSerialNumber;

            string text = doc.Strings.GetValue(GfaInfo.DocSection, GfaInfo.CategoriesEntry);
            var loaded = string.IsNullOrWhiteSpace(text)
                ? new List<UseCategory>()
                : text.Split('\n').Select(UseCategory.Deserialize).Where(c => c != null).ToList();
            _categories = loaded.Count > 0 ? loaded : UseCategory.Defaults();

            Mode = doc.Strings.GetValue(GfaInfo.DocSection, ModeEntry) == "layer"
                ? TrackingMode.ByLayer
                : TrackingMode.ByObject;
            TargetMin = ReadNumber(doc, TargetMinEntry);
            TargetMax = ReadNumber(doc, TargetMaxEntry);
        }

        private static IEnumerable<RhinoObject> AllObjects(RhinoDoc doc)
        {
            var settings = new ObjectEnumeratorSettings
            {
                NormalObjects = true,
                LockedObjects = true,
                HiddenObjects = true,
                DeletedObjects = false,
                ReferenceObjects = true,
                IncludeLights = false,
                IncludeGrips = false,
                IncludePhantoms = false,
            };
            return doc.Objects.GetObjectList(settings);
        }
    }
}
