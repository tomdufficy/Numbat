using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Numbat.Commands.Analysis.NumbatAf
{
    /// <summary>
    /// Draws the live result while the nbAF window is open: one filled, outlined area per building face, clash
    /// zones filled in the clash colour with a numbered marker, footprints and plot lines. Depth labels only when
    /// the user turns them on. Nothing is added to the document; nbAFBake writes the outlines as closed curves.
    /// The window turns the preview on when it opens and off when it closes.
    /// </summary>
    internal sealed class AfPreviewConduit : DisplayConduit
    {
        public static readonly AfPreviewConduit Instance = new AfPreviewConduit();

        private readonly List<(Mesh mesh, Element element)> _fills = new List<(Mesh, Element)>();
        private readonly List<(Curve curve, Element element)> _curves = new List<(Curve, Element)>();
        private readonly List<AfLabel> _labels = new List<AfLabel>();
        private readonly List<(Point3d at, string text, bool check)> _markers = new List<(Point3d, string, bool)>();
        private readonly List<Curve> _checks = new List<Curve>();
        private BoundingBox _box = BoundingBox.Empty;

        // Colour by building: made on first use after each update, since it is usually off.
        private AfResult _result;
        private double _metre = 1;
        private List<(Mesh mesh, int building)> _buildingMeshes;
        private List<(Point3d at, string name, int building)> _buildingNames;

        private AfPreviewConduit() { }

        /// <summary>Takes a new result. Geometry is prepared here, so drawing stays cheap.</summary>
        public void Update(AfResult a, RhinoDoc doc)
        {
            _fills.Clear();
            _curves.Clear();
            _labels.Clear();
            _markers.Clear();
            _checks.Clear();
            _box = BoundingBox.Empty;
            _result = a;
            _buildingMeshes = null;
            _buildingNames = null;
            if (a == null || doc == null) return;
            double metre = RhinoMath.UnitScale(UnitSystem.Meters, doc.ModelUnitSystem);
            _metre = metre;
            double tol = doc.ModelAbsoluteTolerance;

            foreach (AfFace f in a.Faces)
            {
                const Element role = Element.Areas;
                foreach (Curve c in f.Regions)
                {
                    foreach (Mesh m in Fill(c, tol)) _fills.Add((m, role));
                    _curves.Add((c, role));
                    _box.Union(c.GetBoundingBox(true));
                }
            }
            _labels.AddRange(a.Labels);
            for (int i = 0; i < a.Buildings.Count; i++)
            {
                foreach (Curve c in a.Footprints[i]) _curves.Add((c, Element.Footprints));
                if (a.Buildings[i].Plot != null)
                {
                    _curves.Add((a.Buildings[i].Plot, Element.Plots));
                    _box.Union(a.Buildings[i].Plot.GetBoundingBox(true));
                }
            }

            // Clash zones sit a hair above the areas so they never flicker against them.
            Transform lift = Transform.Translation(0, 0, 0.02 * metre);
            foreach (Curve road in a.Roads) _curves.Add((road, Element.Roads));
            foreach (Flag f in a.Flags)
            {
                if (f.IsCheck)
                {
                    // Something to check: an amber outline and marker, no fill.
                    Curve check = f.Region.DuplicateCurve();
                    check.Transform(lift);
                    _checks.Add(check);
                    _markers.Add((f.DotAt, f.Number.ToString(), true));
                    continue;
                }
                foreach (Mesh m in Fill(f.Region, tol))
                {
                    m.Transform(lift);
                    _fills.Add((m, Element.Clashes));
                }
                Curve outline = f.Region.DuplicateCurve();
                outline.Transform(lift);
                _curves.Add((outline, Element.Clashes));
                _markers.Add((f.DotAt, f.Number.ToString(), false));
            }
        }

        private static IEnumerable<Mesh> Fill(Curve c, double tol)
        {
            if (c.TryGetPolyline(out Polyline pl))
            {
                Mesh m = Mesh.CreateFromClosedPolyline(pl);
                if (m != null) return new[] { m };
            }
            var meshes = new List<Mesh>();
            foreach (Brep b in Brep.CreatePlanarBreps(c, tol) ?? Array.Empty<Brep>())
            {
                meshes.AddRange(Mesh.CreateFromBrep(b, MeshingParameters.FastRenderMesh) ?? Array.Empty<Mesh>());
            }
            return meshes;
        }

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            if (_box.IsValid) e.IncludeBoundingBox(_box);
        }

        /// <summary>Each building's volumes as one mesh, pushed out 3 cm so it draws over the volumes without flicker.</summary>
        private void MakeBuildingMeshes()
        {
            _buildingMeshes = new List<(Mesh, int)>();
            _buildingNames = new List<(Point3d, string, int)>();
            if (_result == null) return;
            for (int i = 0; i < _result.Buildings.Count; i++)
            {
                BoundingBox box = BoundingBox.Empty;
                foreach (Brep v in _result.Buildings[i].Volumes)
                {
                    var mesh = new Mesh();
                    foreach (Mesh m in Mesh.CreateFromBrep(v, MeshingParameters.FastRenderMesh) ?? Array.Empty<Mesh>()) mesh.Append(m);
                    if (mesh.Vertices.Count == 0) continue;
                    // Grown by 3 cm about its own centre, so it sits just outside the volume and draws over it without flicker.
                    BoundingBox vb = v.GetBoundingBox(true);
                    double size = Math.Max(vb.Diagonal.X, Math.Max(vb.Diagonal.Y, vb.Diagonal.Z));
                    if (size > 0) mesh.Transform(Transform.Scale(vb.Center, 1 + 0.06 * _metre / size));
                    _buildingMeshes.Add((mesh, i));
                    box.Union(vb);
                }
                if (box.IsValid) _buildingNames.Add((new Point3d(box.Center.X, box.Center.Y, box.Max.Z), _result.Buildings[i].Name, i));
            }
        }

        /// <summary>A building's colour, from the colour index AfModel gave it (neighbours never share one).</summary>
        private Color BuildingColour(int building) =>
            AfColours.BuildingColour(_result != null && building < _result.BuildingColours.Length ? _result.BuildingColours[building] : building);

        protected override void PostDrawObjects(DrawEventArgs e)
        {
            if (AfColours.ColourByBuilding)
            {
                if (_buildingMeshes == null) MakeBuildingMeshes();
                foreach (var b in _buildingMeshes) e.Display.DrawMeshShaded(b.mesh, new DisplayMaterial(BuildingColour(b.building), 0.25));
            }
            // Colours are read per frame, so a change in the window shows straight away.
            var colours = AfColours.Elements.ToDictionary(x => x.element, x => AfColours.Get(x.element));
            foreach (var f in _fills)
            {
                double transparency = f.element == Element.Clashes ? 0.35 : 0.65;
                e.Display.DrawMeshShaded(f.mesh, new DisplayMaterial(colours[f.element], transparency));
            }
            foreach (var c in _curves)
            {
                e.Display.DrawCurve(c.curve, colours[c.element], c.element == Element.Clashes ? 3 : 2);
            }
            foreach (Curve c in _checks) e.Display.DrawCurve(c, AfColours.CheckColour, 3);

            // Depth dimensions: a line from the wall to the edge of its area with oblique ticks at both ends, and the
            // depth beside it. They scale with the view; text too small to read on screen is skipped.
            if (!AfColours.ShowLabels) return;
            Color ink = Color.FromArgb(45, 45, 45);
            foreach (AfLabel l in _labels)
            {
                e.Display.DrawLine(l.Start, l.End, ink, 1);
                Vector3d along = l.End - l.Start;
                along.Unitize();
                Vector3d tick = along + Vector3d.CrossProduct(Vector3d.ZAxis, along);
                tick.Unitize();
                tick *= l.Height * 0.6;
                e.Display.DrawLine(l.Start - tick, l.Start + tick, ink, 2);
                e.Display.DrawLine(l.End - tick, l.End + tick, ink, 2);

                if (!e.Viewport.GetWorldToScreenScale(l.At, out double pixelsPerUnit) || l.Height * pixelsPerUnit < MinLabelPixels) continue;
                e.Display.Draw3dText(l.Text, ink, l.Plane, l.Height, "Arial", false, false,
                    TextHorizontalAlignment.Center, TextVerticalAlignment.Middle);
            }
        }

        private const double MinLabelPixels = 6;

        protected override void DrawForeground(DrawEventArgs e)
        {
            if (AfColours.ColourByBuilding && _buildingNames != null)
            {
                foreach (var n in _buildingNames) e.Display.DrawDot(n.at, n.name, BuildingColour(n.building), Color.White);
            }
            Color clash = AfColours.Get(Element.Clashes);
            foreach (var m in _markers) e.Display.DrawDot(m.at, m.text, m.check ? AfColours.CheckColour : clash, Color.White);
        }

        /// <summary>Turns the preview on or off and redraws.</summary>
        public static void Show(bool on, RhinoDoc doc)
        {
            if (Instance.Enabled == on) return;
            Instance.Enabled = on;
            doc?.Views.Redraw();
        }
    }
}
