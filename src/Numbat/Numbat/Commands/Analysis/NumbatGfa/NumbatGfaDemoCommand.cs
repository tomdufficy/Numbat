using System;
using System.Drawing;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Numbat.Commands.Analysis.NumbatGfa
{
    /// <summary>
    /// Demo: builds three apartment blocks of 2, 4 and 6 storeys, one closed box per storey, slightly rotated,
    /// on an "nbGFA demo" layer, to try nbGFA on. Running it again replaces them. Shipped in every build but hidden
    /// (see CLAUDE.md: every tool has a hidden &lt;command&gt;Demo).
    /// Dimensions are in metres and converted to the model's units, so it works in mm and m files alike.
    /// </summary>
    // Hidden: works when typed in full, but doesn't appear in autocomplete, so typing "nb" only offers nbGFA.
    [CommandStyle(Style.Hidden)]
    public class NumbatGfaDemoCommand : Command
    {
        private const string LayerName = "nbGFA demo";
        private const double StoreyHeight = 3.2; // m, floor to floor

        // origin (m), footprint width x depth (m), storeys, rotation (degrees)
        private static readonly (double x, double y, double w, double d, int storeys, double rot)[] Blocks =
        {
            (0,  0, 18, 12, 2,   4),
            (32, 0, 24, 13, 4,  -9),
            (68, 4, 16, 15, 6,  16),
        };

        public override string EnglishName => "nbGFADemo";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            double s = RhinoMath.UnitScale(UnitSystem.Meters, doc.ModelUnitSystem);
            int layer = GetOrAddLayer(doc);
            if (layer < 0)
            {
                RhinoApp.WriteLine($"{GfaInfo.Name}: could not create the \"{LayerName}\" layer.");
                return Result.Failure;
            }

            // Replace boxes from a previous run.
            foreach (RhinoObject old in doc.Objects.FindByLayer(doc.Layers[layer]) ?? Array.Empty<RhinoObject>())
            {
                doc.Objects.Delete(old, true);
            }

            var attr = new ObjectAttributes { LayerIndex = layer };
            int count = 0;
            foreach (var b in Blocks)
            {
                var origin = new Point3d(b.x * s, b.y * s, 0);
                Transform rotate = Transform.Rotation(RhinoMath.ToRadians(b.rot), Vector3d.ZAxis, origin);

                for (int i = 0; i < b.storeys; i++)
                {
                    var box = new BoundingBox(
                        origin.X, origin.Y, i * StoreyHeight * s,
                        origin.X + b.w * s, origin.Y + b.d * s, (i + 1) * StoreyHeight * s);
                    Brep brep = Brep.CreateFromBox(box);
                    brep.Transform(rotate);
                    if (doc.Objects.AddBrep(brep, attr) != Guid.Empty) count++;
                }
            }

            doc.Views.ActiveView?.ActiveViewport.ZoomExtents();
            doc.Views.Redraw();
            RhinoApp.WriteLine($"{GfaInfo.Name}: added {count} storey volumes on layer \"{LayerName}\" " +
                               "(18x12 m x2, 24x13 m x4, 16x15 m x6). Run nbGFA to try them.");
            return Result.Success;
        }

        private static int GetOrAddLayer(RhinoDoc doc)
        {
            Layer existing = doc.Layers.FindName(LayerName);
            if (existing != null) return existing.Index;
            return doc.Layers.Add(new Layer { Name = LayerName, Color = Color.FromArgb(200, 200, 205) });
        }
    }
}
