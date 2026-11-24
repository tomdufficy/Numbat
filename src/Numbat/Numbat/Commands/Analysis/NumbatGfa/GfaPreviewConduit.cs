using System;
using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Numbat.UI;

namespace Numbat.Commands.Analysis.NumbatGfa
{
    /// <summary>
    /// Draws every volume with a use in its use's colour while the nbGFA window is open and its eye is open
    /// (<see cref="KitPreview"/>). Drawn over the volume rather than set as its object colour, so it shows in every
    /// display mode (Arctic, Pen, Rendered, ...), leaves the user's objects unchanged and can be hidden. Selected
    /// volumes are left out, so the selection stays visible.
    /// </summary>
    internal sealed class GfaPreviewConduit : DisplayConduit
    {
        public const string Tool = "gfa";

        public static readonly GfaPreviewConduit Instance = new GfaPreviewConduit();

        // Meshes per object, rebuilt when the object changes (new runtime serial number).
        private readonly Dictionary<Guid, (uint serial, Mesh mesh)> _meshes = new Dictionary<Guid, (uint, Mesh)>();
        private IReadOnlyList<(Guid id, Color colour)> _painted = new List<(Guid, Color)>();

        private GfaPreviewConduit() { }

        /// <summary>Takes the volumes and colours of the latest update.</summary>
        public void Update(IReadOnlyList<(Guid id, Color colour)> painted)
        {
            _painted = painted ?? new List<(Guid, Color)>();
            if (_meshes.Count > 4 * Math.Max(16, _painted.Count)) _meshes.Clear(); // forget deleted objects now and then
        }

        protected override void PostDrawObjects(DrawEventArgs e)
        {
            RhinoDoc doc = e.RhinoDoc;
            if (doc == null) return;
            foreach (var (id, colour) in _painted)
            {
                RhinoObject obj = doc.Objects.FindId(id);
                if (obj == null || !obj.Visible || obj.IsSelected(false) > 0) continue;
                Layer layer = obj.Attributes.LayerIndex >= 0 && obj.Attributes.LayerIndex < doc.Layers.Count ? doc.Layers[obj.Attributes.LayerIndex] : null;
                if (layer != null && !layer.IsVisible) continue;
                Mesh mesh = MeshOf(obj, doc);
                if (mesh != null) e.Display.DrawMeshShaded(mesh, new DisplayMaterial(colour, 0.15));
            }
        }

        /// <summary>The volume as one mesh, grown by 3 cm about its centre so it draws over the volume without flicker.</summary>
        private Mesh MeshOf(RhinoObject obj, RhinoDoc doc)
        {
            if (_meshes.TryGetValue(obj.Id, out var hit) && hit.serial == obj.RuntimeSerialNumber) return hit.mesh;
            var mesh = new Mesh();
            Mesh[] render = obj.GetMeshes(MeshType.Render);
            if (render != null && render.Length > 0)
            {
                foreach (Mesh m in render) mesh.Append(m);
            }
            else
            {
                Brep brep = obj.Geometry is Extrusion ex ? ex.ToBrep() : obj.Geometry as Brep;
                foreach (Mesh m in brep != null ? Mesh.CreateFromBrep(brep, MeshingParameters.FastRenderMesh) ?? Array.Empty<Mesh>() : Array.Empty<Mesh>())
                    mesh.Append(m);
            }
            if (mesh.Vertices.Count == 0)
            {
                _meshes[obj.Id] = (obj.RuntimeSerialNumber, null);
                return null;
            }
            BoundingBox box = mesh.GetBoundingBox(true);
            double size = Math.Max(box.Diagonal.X, Math.Max(box.Diagonal.Y, box.Diagonal.Z));
            double grow = 0.06 * RhinoMath.UnitScale(UnitSystem.Meters, doc.ModelUnitSystem);
            if (size > 0) mesh.Transform(Transform.Scale(box.Center, 1 + grow / size));
            _meshes[obj.Id] = (obj.RuntimeSerialNumber, mesh);
            return mesh;
        }

        /// <summary>Shows the colours when the window is open and its eye is open; otherwise hides them.</summary>
        public static void Sync(bool windowOpen)
        {
            bool on = windowOpen && KitPreview.IsShown(Tool);
            if (Instance.Enabled == on) return;
            Instance.Enabled = on;
            RhinoDoc.ActiveDoc?.Views.Redraw();
        }
    }
}
