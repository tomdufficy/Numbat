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
        private readonly List<(Point3d at, string text)> _markers = new List<(Point3d, string)>();
        private BoundingBox _box = BoundingBox.Empty;

        private AfPreviewConduit() { }

        /// <summary>Takes a new result. Geometry is prepared here, so drawing stays cheap.</summary>
        public void Update(AfResult a, RhinoDoc doc)
        {
            _fills.Clear();
            _curves.Clear();
            _labels.Clear();
            _markers.Clear();
            _box = BoundingBox.Empty;
            if (a == null || doc == null) return;
            double metre = RhinoMath.UnitScale(UnitSystem.Meters, doc.ModelUnitSystem);
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
            foreach (Flag f in a.Flags)
            {
                foreach (Mesh m in Fill(f.Region, tol))
                {
                    m.Transform(lift);
                    _fills.Add((m, Element.Clashes));
                }
                Curve outline = f.Region.DuplicateCurve();
                outline.Transform(lift);
                _curves.Add((outline, Element.Clashes));
                _markers.Add((f.DotAt, f.Number.ToString()));
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

        protected override void PostDrawObjects(DrawEventArgs e)
        {
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
            Color clash = AfColours.Get(Element.Clashes);
            foreach (var m in _markers) e.Display.DrawDot(m.at, m.text, clash, Color.White);
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
