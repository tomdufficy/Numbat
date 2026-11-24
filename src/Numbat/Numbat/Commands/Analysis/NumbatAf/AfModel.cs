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
        public int Flags;
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
            if (IsMember(e.OldAttributes) || IsMember(e.NewAttributes) || _plotIds.Contains(e.RhinoObject.Id)) Schedule();
        }

        private void OnDocument(object sender, EventArgs e) => Schedule();

        private void Touch(RhinoObject obj)
        {
            if (_writing || obj == null) return;
            if (IsMember(obj.Attributes) || _plotIds.Contains(obj.Id)) Schedule();
        }

        internal static bool IsMember(ObjectAttributes a) => !string.IsNullOrEmpty(a?.GetUserString(AfInfo.RoleKey));

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
            foreach (RhinoObject obj in doc.Objects.GetObjectList(new ObjectEnumeratorSettings { HiddenObjects = true, LockedObjects = true }))
            {
                if (!IsMember(obj.Attributes)) continue;
                Brep b = Brep.TryConvertBrep(obj.Geometry);
                if (b == null || !b.IsSolid) continue;
                members.Add(obj);
                breps.Add(b);
                groups.Add(obj.Attributes.GetGroupList() ?? new int[0]);
            }

            double tol = doc.ModelAbsoluteTolerance;
            var found = AfBuildings.Group(breps, groups, tol)
                .Select(g => new { Members = g, Box = Union(g.Select(i => breps[i].GetBoundingBox(true))) })
                .OrderBy(g => g.Box.Min.X).ThenBy(g => g.Box.Min.Y) // numbered west to east
                .ToList();

            var buildings = new List<Building>();
            var inputs = new List<BuildingInput>();
            foreach (var g in found)
            {
                var b = new Building { Index = buildings.Count, Name = $"B{buildings.Count + 1}" };
                var input = new BuildingInput { Name = b.Name };
                foreach (int i in g.Members)
                {
                    RhinoObject obj = members[i];
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

            AfResult a = AfSolver.Run(inputs, Rules, doc);
            foreach (Building b in buildings) b.Flags = a.FlagCount(b.Index);

            Buildings = buildings;
            Current = a;
            _plotIds = new HashSet<Guid>(buildings.Where(b => b.Plot != Guid.Empty).Select(b => b.Plot));
            AfPreviewConduit.Instance.Update(a, doc);
            doc.Views.Redraw();
            Changed?.Invoke(this, EventArgs.Empty);
        }

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
