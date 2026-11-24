using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Numbat.Commands.Analysis.NumbatAf
{
    /// <summary>A building as the window shows it: touching (or grouped) volumes, their role and plot.</summary>
    internal sealed class Building
    {
        public int Index;
        public string Name;             // B1, B2, ... numbered west to east
        public List<Guid> Volumes = new List<Guid>();
        public Guid Plot = Guid.Empty;  // empty, or the plot curve's id
        public bool PlotMissing;        // a plot was set but its curve is gone
        public int ColourIndex;         // in AfColours.BuildingPalette; neighbours never share one
        public int Flags;
    }

    /// <summary>Two buildings that almost touch: the closest pair of their volumes and the gap between them.</summary>
    internal sealed class NearMiss
    {
        public Building A, B;
        public Guid VolumeA, VolumeB;
        public double GapM;

        public string GapText => GapM < 0.001 ? $"{GapM * 1000:0.##} mm" : GapM < 0.01 ? $"{GapM * 1000:0.#} mm" : $"{GapM * 100:0.#} cm";
    }

    /// <summary>
    /// nbAF's live state for the active document. The truth lives on the objects: tracked volumes carry the user
    /// string <see cref="AfInfo.RoleKey"/> (and <see cref="AfInfo.PlotKey"/> for their plot), so it is saved with
    /// the file and survives copy and paste. Volumes are grouped into buildings by touch (or Rhino group).
    /// While the window is open (<see cref="Live"/>), any change to a tracked volume or a plot curve recalculates
    /// shortly after. Results are only drawn by <see cref="AfPreviewConduit"/>; nothing is written to the document
    /// until nbAFBake.
    /// </summary>
    internal sealed class AfModel
    {
        /// <summary>Value of the role user string on tracked volumes. Older files may say "target" or "context"; any value counts.</summary>
        public const string Tracked = "tracked";

        private const double DelaySeconds = 0.3;

        private bool _attached, _writing, _pending;
        private DateTime _dirtyAt;
        private HashSet<Guid> _plotIds = new HashSet<Guid>();

        public List<Building> Buildings { get; private set; } = new List<Building>();
        public AfResult Current { get; private set; }
        public AfRules Rules { get; private set; } = new AfRules();

        /// <summary>Buildings closer than <see cref="AfRules.NearMissM"/> but further apart than the join gap.</summary>
        public List<NearMiss> NearMisses { get; private set; } = new List<NearMiss>();

        /// <summary>Tracked objects that aren't closed volumes, so they are left out.</summary>
        public List<Guid> OpenVolumes { get; private set; } = new List<Guid>();

        /// <summary>Curves marked as road centre lines.</summary>
        public List<Guid> Roads { get; private set; } = new List<Guid>();

        /// <summary>The volumes of each building, by building index, as nbAF sees them (for colouring by building).</summary>
        public List<List<Brep>> BuildingVolumes { get; private set; } = new List<List<Brep>>();

        /// <summary>Curved wall faces skipped in the last calculation (not handled yet).</summary>
        public int SkippedFaces => Current?.SkippedFaces ?? 0;

        /// <summary>Recalculate on document changes. True while the window is open.</summary>
        public bool Live { get; set; }

        /// <summary>Raised after every recalculation.</summary>
        public event EventHandler Changed;

        // ---- document events -----------------------------------------------------------

        public void Attach()
        {
            if (_attached) return;
            _attached = true;
            RhinoDoc.AddRhinoObject += OnObject;
            RhinoDoc.DeleteRhinoObject += OnObject;
            RhinoDoc.UndeleteRhinoObject += OnObject;
            RhinoDoc.ReplaceRhinoObject += OnReplace;
            RhinoDoc.ModifyObjectAttributes += OnAttributes;
            RhinoDoc.EndOpenDocument += OnDocument;
            RhinoDoc.NewDocument += OnDocument;
            RhinoDoc.ActiveDocumentChanged += OnDocument;
            RhinoDoc.DocumentPropertiesChanged += OnDocument; // units
        }

        public void Detach()
        {
            if (!_attached) return;
            _attached = false;
            RhinoDoc.AddRhinoObject -= OnObject;
            RhinoDoc.DeleteRhinoObject -= OnObject;
            RhinoDoc.UndeleteRhinoObject -= OnObject;
            RhinoDoc.ReplaceRhinoObject -= OnReplace;
            RhinoDoc.ModifyObjectAttributes -= OnAttributes;
            RhinoDoc.EndOpenDocument -= OnDocument;
            RhinoDoc.NewDocument -= OnDocument;
            RhinoDoc.ActiveDocumentChanged -= OnDocument;
            RhinoDoc.DocumentPropertiesChanged -= OnDocument;
            RhinoApp.Idle -= OnIdle;
            _pending = false;
        }

        private void OnObject(object sender, RhinoObjectEventArgs e) => Touch(e.TheObject);

        private void OnReplace(object sender, RhinoReplaceObjectEventArgs e)
        {
            Touch(e.OldRhinoObject);
            Touch(e.NewRhinoObject);
        }

        private void OnAttributes(object sender, RhinoModifyObjectAttributesEventArgs e)
        {
            if (_writing) return;
            if (IsMember(e.OldAttributes) || IsMember(e.NewAttributes) || IsRoad(e.OldAttributes) || IsRoad(e.NewAttributes) ||
                _plotIds.Contains(e.RhinoObject.Id)) Schedule();
        }

        private void OnDocument(object sender, EventArgs e) => Schedule();

        private void Touch(RhinoObject obj)
        {
            if (_writing || obj == null) return;
            if (IsMember(obj.Attributes) || IsRoad(obj.Attributes) || _plotIds.Contains(obj.Id)) Schedule();
        }

        internal static bool IsMember(ObjectAttributes a) => !string.IsNullOrEmpty(a?.GetUserString(AfInfo.RoleKey));

        internal static bool IsRoad(ObjectAttributes a) => !string.IsNullOrEmpty(a?.GetUserString(AfInfo.RoadKey));

        /// <summary>Recalculate a moment after the last change, so dragging and multi-object edits stay smooth.</summary>
        public void Schedule()
        {
            if (!Live) return;
            _dirtyAt = DateTime.UtcNow;
            if (_pending) return;
            _pending = true;
            RhinoApp.Idle += OnIdle;
        }

        private void OnIdle(object sender, EventArgs e)
        {
            if ((DateTime.UtcNow - _dirtyAt).TotalSeconds < DelaySeconds) return;
            RhinoApp.Idle -= OnIdle;
            _pending = false;
            if (Live) Refresh(RhinoDoc.ActiveDoc);
        }

        // ---- recalculation -------------------------------------------------------------

        /// <summary>Rebuilds the buildings and runs the checks. Reads the document only.</summary>
        public void Refresh(RhinoDoc doc)
        {
            if (doc == null) return;
            Rules = AfRules.Load(doc);

            var members = new List<RhinoObject>();
            var breps = new List<Brep>();
            var groups = new List<int[]>();
            var open = new List<Guid>();
            var roads = new List<Guid>();
            var roadCurves = new List<Curve>();
            foreach (RhinoObject obj in doc.Objects.GetObjectList(new ObjectEnumeratorSettings { HiddenObjects = true, LockedObjects = true }))
            {
                if (IsRoad(obj.Attributes) && obj.Geometry is Curve road)
                {
                    roads.Add(obj.Id);
                    roadCurves.Add(road.DuplicateCurve());
                    continue;
                }
                if (!IsMember(obj.Attributes)) continue;
                Brep b = Brep.TryConvertBrep(obj.Geometry);
                if (b == null || !b.IsSolid)
                {
                    open.Add(obj.Id);
                    continue;
                }
                members.Add(obj);
                breps.Add(b);
                groups.Add(obj.Attributes.GetGroupList() ?? new int[0]);
            }

            // Pairs the user marked as separate buildings ("Keep apart").
            var index = new Dictionary<Guid, int>();
            for (int i = 0; i < members.Count; i++) index[members[i].Id] = i;
            var apart = new HashSet<(int, int)>();
            for (int i = 0; i < members.Count; i++)
            {
                foreach (string id in (members[i].Attributes.GetUserString(AfInfo.ApartKey) ?? "").Split(','))
                {
                    if (Guid.TryParse(id, out Guid other) && index.TryGetValue(other, out int j)) apart.Add((i, j));
                }
            }

            double tol = doc.ModelAbsoluteTolerance;
            double metre = RhinoMath.UnitScale(UnitSystem.Meters, doc.ModelUnitSystem);
            var found = AfBuildings.Group(breps, groups, Rules.JoinGapMm / 1000 * metre, AfRules.NearMissM * metre, tol, apart,
                    out List<VolumePair> close)
                .Select(g => new { Members = g, Box = Union(g.Select(i => breps[i].GetBoundingBox(true))) })
                .OrderBy(g => g.Box.Min.X).ThenBy(g => g.Box.Min.Y) // numbered west to east
                .ToList();

            var buildings = new List<Building>();
            var inputs = new List<BuildingInput>();
            var buildingOf = new Dictionary<int, Building>();
            foreach (var g in found)
            {
                var b = new Building { Index = buildings.Count, Name = $"B{buildings.Count + 1}" };
                var input = new BuildingInput { Name = b.Name };
                foreach (int i in g.Members)
                {
                    RhinoObject obj = members[i];
                    buildingOf[i] = b;
                    b.Volumes.Add(obj.Id);
                    input.Volumes.Add(breps[i]);
                    if (b.Plot == Guid.Empty && Guid.TryParse(obj.Attributes.GetUserString(AfInfo.PlotKey), out Guid plot)) b.Plot = plot;
                }
                if (b.Plot != Guid.Empty)
                {
                    input.Plot = (doc.Objects.FindId(b.Plot)?.Geometry as Curve)?.DuplicateCurve();
                    b.PlotMissing = input.Plot == null;
                }
                buildings.Add(b);
                inputs.Add(input);
            }

            AfResult a = AfSolver.Run(inputs, Rules, doc, roadCurves);
            AssignColours(buildings, a, metre);

            var nearMisses = close
                .Select(p => buildingOf[p.A].Index <= buildingOf[p.B].Index ? p : new VolumePair { A = p.B, B = p.A, Gap = p.Gap }) // B3/B7, not B7/B3
                .Select(p => new NearMiss
                {
                    A = buildingOf[p.A], B = buildingOf[p.B], VolumeA = members[p.A].Id, VolumeB = members[p.B].Id, GapM = p.Gap / metre,
                }).Where(m => m.A != m.B).OrderBy(m => m.A.Index).ThenBy(m => m.B.Index).ToList();

            // A clash between two buildings that almost touch is probably a modelling gap: say so.
            foreach (Flag f in a.Flags)
            {
                NearMiss m = nearMisses.FirstOrDefault(x => f.Buildings.Contains(x.A.Index) && f.Buildings.Contains(x.B.Index));
                if (m != null) f.Note = $"{m.A.Name} and {m.B.Name} are {m.GapText} apart: one building?";
            }
            foreach (Building b in buildings) b.Flags = a.FlagCount(b.Index);
            NearMisses = nearMisses;
            OpenVolumes = open;
            Roads = roads;
            BuildingVolumes = a.Buildings.Select(x => x.Volumes).ToList();

            Buildings = buildings;
            Current = a;
            _plotIds = new HashSet<Guid>(buildings.Where(b => b.Plot != Guid.Empty).Select(b => b.Plot));
            a.BuildingColours = buildings.Select(b => b.ColourIndex).ToArray();
            AfPreviewConduit.Instance.Update(a, doc);
            doc.Views.Redraw();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gives every building a colour no building within 40 m has (graph colouring, most crowded buildings first), so
        /// Colour by building never shows two neighbours alike. With more neighbours than colours, the colour least used
        /// among them.
        /// </summary>
        private static void AssignColours(List<Building> buildings, AfResult a, double metre)
        {
            int n = buildings.Count, colours = AfColours.BuildingPalette.Length;
            double reach = 40 * metre;
            var boxes = a.Footprints.Select(f => { BoundingBox b = BoundingBox.Empty; foreach (Curve c in f) b.Union(c.GetBoundingBox(true)); b.Inflate(reach); return b; }).ToList();
            var near = Enumerable.Range(0, n).Select(_ => new List<int>()).ToList();
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (!boxes[i].IsValid || !boxes[j].IsValid || !Overlap2d(boxes[i], boxes[j])) continue;
                    double gap = double.MaxValue;
                    foreach (Curve ci in a.Footprints[i])
                    {
                        foreach (Curve cj in a.Footprints[j])
                        {
                            if (ci.ClosestPoints(cj, out Point3d pi, out Point3d pj)) gap = Math.Min(gap, pi.DistanceTo(pj));
                        }
                    }
                    if (gap > reach) continue;
                    near[i].Add(j);
                    near[j].Add(i);
                }
            }
            var colour = Enumerable.Repeat(-1, n).ToArray();
            var used = new int[colours];
            foreach (int i in Enumerable.Range(0, n).OrderByDescending(i => near[i].Count).ThenBy(i => i))
            {
                var taken = near[i].Where(j => colour[j] >= 0).GroupBy(j => colour[j]).ToDictionary(g => g.Key, g => g.Count());
                colour[i] = Enumerable.Range(0, colours)
                    .OrderBy(c => taken.TryGetValue(c, out int k) ? k : 0) // free colours first
                    .ThenBy(c => used[c])                                  // then the least used overall
                    .ThenBy(c => c)
                    .First();
                used[colour[i]]++;
            }
            for (int i = 0; i < n; i++) buildings[i].ColourIndex = colour[i];
        }

        private static bool Overlap2d(BoundingBox a, BoundingBox b) =>
            a.Min.X <= b.Max.X && b.Min.X <= a.Max.X && a.Min.Y <= b.Max.Y && b.Min.Y <= a.Max.Y;

        private static BoundingBox Union(IEnumerable<BoundingBox> boxes)
        {
            BoundingBox box = BoundingBox.Empty;
            foreach (BoundingBox b in boxes) box.Union(b);
            return box;
        }

        // ---- edits (window and commands) ---------------------------------------------------
        // Each edit is one undo step. Undo of a role or plot change triggers a recalculation through the
        // attribute event, so the window follows.

        /// <summary>Starts tracking these volumes.</summary>
        public int Add(RhinoDoc doc, IEnumerable<RhinoObject> volumes)
        {
            int n = 0;
            Edit(doc, "add volumes", () =>
            {
                foreach (RhinoObject obj in volumes)
                {
                    if (IsMember(obj.Attributes)) continue;
                    SetStrings(doc, obj, Tracked, null);
                    n++;
                }
            });
            return n;
        }

        /// <summary>Stops tracking these volumes (removes nbAF's user strings). The objects stay.</summary>
        public int Remove(RhinoDoc doc, IEnumerable<RhinoObject> volumes)
        {
            int n = 0;
            Edit(doc, "remove volumes", () =>
            {
                foreach (RhinoObject obj in volumes)
                {
                    if (!IsMember(obj.Attributes)) continue;
                    var attr = obj.Attributes.Duplicate();
                    attr.DeleteUserString(AfInfo.RoleKey);
                    attr.DeleteUserString(AfInfo.PlotKey);
                    Modify(doc, obj, attr);
                    n++;
                }
            });
            return n;
        }

        /// <param name="plot">The plot curve, or null to clear it.</param>
        public void SetPlot(RhinoDoc doc, Building building, RhinoObject plot) =>
            Edit(doc, plot == null ? $"clear plot of {building.Name}" : $"set plot of {building.Name}", () =>
            {
                foreach (Guid id in building.Volumes)
                {
                    RhinoObject obj = doc.Objects.FindId(id);
                    if (obj != null) SetStrings(doc, obj, null, plot?.Id ?? Guid.Empty);
                }
            });

        /// <summary>Marks curves as road centre lines.</summary>
        public int AddRoads(RhinoDoc doc, IEnumerable<RhinoObject> curves)
        {
            int n = 0;
            Edit(doc, "add road centre lines", () =>
            {
                foreach (RhinoObject obj in curves)
                {
                    if (!(obj.Geometry is Curve) || IsRoad(obj.Attributes)) continue;
                    var attr = obj.Attributes.Duplicate();
                    attr.SetUserString(AfInfo.RoadKey, "centre");
                    Modify(doc, obj, attr);
                    n++;
                }
            });
            return n;
        }

        /// <summary>Stops using a curve as a road centre line. The curve stays.</summary>
        public void RemoveRoad(RhinoDoc doc, Guid id) =>
            Edit(doc, "remove road centre line", () =>
            {
                RhinoObject obj = doc.Objects.FindId(id);
                if (obj == null) return;
                var attr = obj.Attributes.Duplicate();
                attr.DeleteUserString(AfInfo.RoadKey);
                Modify(doc, obj, attr);
            });

        /// <summary>Makes two almost-touching buildings one, by putting the two closest volumes in a Rhino group.</summary>
        public void JoinBuildings(RhinoDoc doc, NearMiss m) =>
            Edit(doc, $"join {m.A.Name} and {m.B.Name}", () =>
            {
                int group = doc.Groups.Add(Unique(doc, "nbAF join"), new[] { m.VolumeA, m.VolumeB });
                if (group < 0) RhinoApp.WriteLine($"{AfInfo.Name}: could not group {m.A.Name} and {m.B.Name}.");
            });

        /// <summary>Remembers that two almost-touching buildings are separate, so nbAF stops asking.</summary>
        public void KeepApart(RhinoDoc doc, NearMiss m) =>
            Edit(doc, $"keep {m.A.Name} and {m.B.Name} apart", () =>
            {
                RhinoObject obj = doc.Objects.FindId(m.VolumeA);
                if (obj == null) return;
                var ids = (obj.Attributes.GetUserString(AfInfo.ApartKey) ?? "").Split(',').Where(x => x.Length > 0).ToList();
                if (!ids.Contains(m.VolumeB.ToString())) ids.Add(m.VolumeB.ToString());
                var attr = obj.Attributes.Duplicate();
                attr.SetUserString(AfInfo.ApartKey, string.Join(",", ids));
                Modify(doc, obj, attr);
            });

        private static string Unique(RhinoDoc doc, string name)
        {
            if (doc.Groups.Find(name) < 0) return name;
            for (int i = 2; ; i++) if (doc.Groups.Find($"{name} {i}") < 0) return $"{name} {i}";
        }

        public void SaveRules(RhinoDoc doc, AfRules rules)
        {
            rules.Save(doc);
            Refresh(doc);
        }

        /// <summary>Runs an edit as one undo step and recalculates afterwards.</summary>
        private void Edit(RhinoDoc doc, string what, Action action)
        {
            uint undo = doc.UndoRecordingIsActive ? 0 : doc.BeginUndoRecord($"{AfInfo.Name}: {what}");
            try
            {
                action();
            }
            finally
            {
                if (undo != 0) doc.EndUndoRecord(undo);
            }
            Refresh(doc);
        }

        /// <param name="role">New role, or null to keep it.</param>
        /// <param name="plot">New plot id (Guid.Empty clears it), or null to keep it.</param>
        private void SetStrings(RhinoDoc doc, RhinoObject obj, string role, Guid? plot)
        {
            var attr = obj.Attributes.Duplicate();
            if (role != null) attr.SetUserString(AfInfo.RoleKey, role);
            if (plot.HasValue)
            {
                if (plot.Value == Guid.Empty) attr.DeleteUserString(AfInfo.PlotKey);
                else attr.SetUserString(AfInfo.PlotKey, plot.Value.ToString());
            }
            Modify(doc, obj, attr);
        }

        private void Modify(RhinoDoc doc, RhinoObject obj, ObjectAttributes attr)
        {
            _writing = true;
            try
            {
                doc.Objects.ModifyAttributes(obj, attr, true);
            }
            finally
            {
                _writing = false;
            }
        }

        public List<RhinoObject> VolumesOf(RhinoDoc doc, Building b) =>
            b.Volumes.Select(id => doc.Objects.FindId(id)).Where(o => o != null).ToList();

        public Building Find(string name) =>
            Buildings.FirstOrDefault(x => string.Equals(x.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
