using System;
using System.Globalization;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Numbat.Commands.Analysis.NumbatAf
{
    /// <summary>
    /// Writes a result to sublayers of the "nbAF" layer, one per <see cref="Element"/> plus labels. Called only from
    /// nbAFBake and nbAFClear, so every change is one undo step. Baking again replaces the previous output.
    /// Existing layers keep their properties; new ones take the colours from <see cref="AfColours"/>.
    /// </summary>
    internal static class AfBake
    {
        private const string Labels = "Labels";

        private static string[] AllLayers => AfColours.Elements.Select(e => e.layer).Concat(new[] { Labels }).ToArray();

        /// <summary>Deletes all objects on the nbAF output layers. Returns how many were deleted.</summary>
        public static int Clear(RhinoDoc doc)
        {
            int count = 0;
            foreach (string name in AllLayers)
            {
                int index = doc.Layers.FindByFullPath($"{AfInfo.OutputLayer}::{name}", -1);
                if (index < 0) continue;
                var settings = new ObjectEnumeratorSettings { LayerIndexFilter = index, HiddenObjects = true, LockedObjects = true };
                foreach (RhinoObject obj in doc.Objects.GetObjectList(settings).ToList())
                {
                    if (doc.Objects.Delete(obj.Id, true)) count++;
                }
            }
            return count;
        }

        /// <summary>Adds the result's objects. Returns how many were added, or -1 if a layer could not be made.</summary>
        public static int Write(RhinoDoc doc, AfResult a)
        {
            double metre = RhinoMath.UnitScale(UnitSystem.Meters, doc.ModelUnitSystem);

            ObjectAttributes target = Attr(doc, AfColours.Layer(Element.Target));
            ObjectAttributes context = Attr(doc, AfColours.Layer(Element.Context));
            ObjectAttributes labels = Attr(doc, Labels);
            ObjectAttributes clashes = Attr(doc, AfColours.Layer(Element.Clashes));
            ObjectAttributes plots = Attr(doc, AfColours.Layer(Element.Plots));
            ObjectAttributes footprints = Attr(doc, AfColours.Layer(Element.Footprints));
            if (new[] { target, context, labels, clashes, plots, footprints }.Any(x => x == null)) return -1;
            clashes.DisplayOrder = 1; // flags draw over labels

            int n = 0;
            void Count(Guid id) { if (id != Guid.Empty) n++; }
            ObjectAttributes Named(ObjectAttributes attr, string name)
            {
                ObjectAttributes copy = attr.Duplicate();
                copy.Name = name;
                return copy;
            }

            // One closed curve per building face (per region when a face has parts that don't meet).
            foreach (AfFace f in a.Faces)
            {
                BuildingInput b = a.Buildings[f.Building];
                string name = $"{b.Name} {f.Facing}, depth {Range(f.DepthMin, f.DepthMax, metre)} m";
                foreach (Curve c in f.Regions) Count(doc.Objects.AddCurve(c, Named(b.Target ? target : context, name)));
            }

            for (int i = 0; i < a.Buildings.Count; i++)
            {
                BuildingInput b = a.Buildings[i];
                foreach (Curve c in a.Footprints[i]) Count(doc.Objects.AddCurve(c, Named(footprints, $"{b.Name} footprint")));
                if (b.Plot != null) Count(doc.Objects.AddCurve(b.Plot, Named(plots, $"{b.Name} plot")));
            }

            // Clashes: the conflict zone as a closed curve, and a numbered marker that names it on hover.
            foreach (Flag f in a.Flags)
            {
                Count(doc.Objects.AddCurve(f.Region, Named(clashes, $"Clash {f.Number}: {f.Describe()}")));
                Count(doc.Objects.AddTextDot(new TextDot(f.Number.ToString(), f.DotAt) { FontHeight = 13, SecondaryText = f.Describe() }, clashes));
            }

            if (AfColours.ShowLabels)
            {
                // Real aligned dimensions from the wall to the edge of its area, in the document's current dimension
                // style, with the text set to the depth in metres. The object name says how H was worked out.
                foreach (AfLabel l in a.Labels)
                {
                    Vector3d along = l.End - l.Start;
                    along.Unitize();
                    // The dimension's plane must run along what it measures, or Rhino collapses it to zero length.
                    var plane = new Plane(l.Start, along, Vector3d.CrossProduct(Vector3d.ZAxis, along));
                    LinearDimension dim = LinearDimension.Create(AnnotationType.Aligned, doc.DimStyles.Current,
                        plane, along, l.Start, l.End, l.Start, 0);
                    if (dim == null) continue;
                    dim.PlainText = l.Text;
                    dim.TextHeight = l.Height;
                    Count(doc.Objects.AddLinearDimension(dim, Named(labels, Detail(l.Wall, metre))));
                }
            }
            return n;
        }

        private static string M(double v, double metre) => (v / metre).ToString("0.00", CultureInfo.InvariantCulture);

        private static string Range(double a, double b, double metre) =>
            Math.Abs(a - b) < 0.005 * metre ? M(a, metre) : $"{M(Math.Min(a, b), metre)}–{M(Math.Max(a, b), metre)}";

        public static string DepthText(Wall w, double metre) => $"{Range(w.DepthStart, w.DepthEnd, metre)} m";

        /// <summary>How the depth was worked out; shown when hovering a baked label dot.</summary>
        public static string Detail(Wall w, double metre)
        {
            if (w.Gable)
            {
                return $"Depth {Range(w.DepthStart, w.DepthEnd, metre)} m, gable: H {Range(w.HStart, w.HEnd, metre)} m " +
                       "(wall height up to the ridge, following the gable)";
            }
            string h = w.RoofHeight > 0
                ? $"H {M(w.H, metre)} m = wall {M(w.WallHeight, metre)} + {(w.RoofShare < 1 ? "share" : "all")} of roof " +
                  $"{M(w.RoofHeight, metre)} ({w.RoofPitchDeg:0}°)"
                : $"H {M(w.H, metre)} m (flat roof)";
            return $"Depth {M(w.Depth, metre)} m, {h}";
        }

        private static ObjectAttributes Attr(RhinoDoc doc, string name)
        {
            int index = Layer(doc, name);
            return index < 0 ? null : new ObjectAttributes { LayerIndex = index };
        }

        private static int Layer(RhinoDoc doc, string name)
        {
            int parent = doc.Layers.FindByFullPath(AfInfo.OutputLayer, -1);
            if (parent < 0) parent = doc.Layers.Add(new Layer { Name = AfInfo.OutputLayer, Color = System.Drawing.Color.FromArgb(199, 156, 105) });
            if (parent < 0) return -1;

            int index = doc.Layers.FindByFullPath($"{AfInfo.OutputLayer}::{name}", -1);
            if (index >= 0) return index;

            System.Drawing.Color colour = name == Labels ? System.Drawing.Color.FromArgb(110, 110, 110)
                                        : AfColours.Get(AfColours.Elements.First(e => e.layer == name).element);
            return doc.Layers.Add(new Layer { Name = name, Color = colour, PlotColor = colour, ParentLayerId = doc.Layers[parent].Id });
        }
    }
}
