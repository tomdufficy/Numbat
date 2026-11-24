using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Numbat.Commands.Analysis.NumbatAf
{
    /// <summary>
    /// Demo: builds <see cref="AfDemoScene"/> on an "nbAF demo" layer and registers the volumes with nbAF (block B as
    /// the target, every building with its own plot), so nbAF can be tried straight away. Running it again replaces
    /// the layer's contents. Shipped in every build but hidden. Dimensions are in metres and converted to the model's
    /// units, so it works in mm and m files alike.
    /// </summary>
    // Hidden: works when typed in full, but doesn't appear in autocomplete, so typing "nb" only offers nbAF.
    [CommandStyle(Style.Hidden)]
    public class NumbatAfDemoCommand : Command
    {
        private const string LayerName = "nbAF demo";

        public override string EnglishName => "nbAFDemo";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            int layer = GetOrAddLayer(doc);
            if (layer < 0)
            {
                RhinoApp.WriteLine($"{AfInfo.Name}: could not create the \"{LayerName}\" layer.");
                return Result.Failure;
            }
            foreach (RhinoObject old in doc.Objects.FindByLayer(doc.Layers[layer]) ?? Array.Empty<RhinoObject>())
            {
                doc.Objects.Delete(old, true);
            }

            var scene = AfDemoScene.Build(RhinoMath.UnitScale(UnitSystem.Meters, doc.ModelUnitSystem));
            int count = 0;
            foreach (AfDemoScene.DemoBuilding b in scene.Buildings)
            {
                Guid plot = doc.Objects.AddCurve(b.Plot, new ObjectAttributes { LayerIndex = layer, Name = $"Plot {b.Label}" });
                foreach (Brep v in b.Volumes)
                {
                    var attr = new ObjectAttributes { LayerIndex = layer, Name = $"Block {b.Label}" };
                    attr.SetUserString(AfInfo.RoleKey, b.Target ? "target" : "context");
                    attr.SetUserString(AfInfo.PlotKey, plot.ToString());
                    if (doc.Objects.AddBrep(v, attr) != Guid.Empty) count++;
                }
            }

            doc.Views.ActiveView?.ActiveViewport.ZoomExtents();
            doc.Views.Redraw();
            RhinoApp.WriteLine($"{AfInfo.Name}: added {count} storey volumes (blocks A–D, each with a plot 4 m around it) " +
                               $"on layer \"{LayerName}\". Block B is the target. Run nbAF to see the Abstandsflächen.");
            return Result.Success;
        }

        private static int GetOrAddLayer(RhinoDoc doc)
        {
            Layer existing = doc.Layers.FindName(LayerName);
            if (existing != null) return existing.Index;
            return doc.Layers.Add(new Layer { Name = LayerName, Color = Color.FromArgb(200, 200, 205) });
        }
    }

    /// <summary>
    /// The demo site, in the shape nbAF expects: one closed box per storey, stacked.
    /// Blocks A–C are nbGFADemo's three apartment blocks (2, 4 and 6 storeys of 3.2 m, slightly rotated).
    /// Block A also gets a 35° gable roof as a separate volume. Block D steps back: its upper storeys cover only
    /// part of the floor below. Each building's plot is its ground-floor footprint offset by 4 m.
    /// Block B is the target. The expected results are listed in CLAUDE.md (nbAF).
    /// </summary>
    internal static class AfDemoScene
    {
        public const double StoreyHeight = 3.2; // m, floor to floor, as in nbGFADemo
        public const double PlotOffset = 4.0;   // m, plot line around the ground floor

        // nbGFADemo's blocks: origin (m), footprint width x depth (m), storeys, rotation (degrees)
        private static readonly (double x, double y, double w, double d, int storeys, double rot)[] Blocks =
        {
            (0,  0, 18, 12, 2,   4),
            (32, 0, 24, 13, 4,  -9),
            (68, 4, 16, 15, 6,  16),
        };

        public sealed class DemoBuilding
        {
            public string Label;
            public bool Target;
            public readonly List<Brep> Volumes = new List<Brep>();
            public Curve Plot;
        }

        public sealed class Scene
        {
            public readonly List<DemoBuilding> Buildings = new List<DemoBuilding>();
        }

        /// <param name="s">One metre in model units.</param>
        public static Scene Build(double s)
        {
            var scene = new Scene();

            // A–C: nbGFADemo's blocks.
            for (int k = 0; k < Blocks.Length; k++)
            {
                var b = Blocks[k];
                var building = new DemoBuilding { Label = ((char)('A' + k)).ToString(), Target = k == 1 };
                var origin = new Point3d(b.x * s, b.y * s, 0);
                Transform rotate = Transform.Rotation(RhinoMath.ToRadians(b.rot), Vector3d.ZAxis, origin);
                for (int i = 0; i < b.storeys; i++)
                {
                    Brep box = Box(b.x, b.y, i * StoreyHeight, b.x + b.w, b.y + b.d, (i + 1) * StoreyHeight, s);
                    box.Transform(rotate);
                    building.Volumes.Add(box);
                }
                building.Plot = Rectangle(b.x - PlotOffset, b.y - PlotOffset, b.x + b.w + PlotOffset, b.y + b.d + PlotOffset, s, rotate);
                scene.Buildings.Add(building);
            }

            // A: 35° gable roof on top, ridge along the 18 m side, as its own volume.
            {
                var b = Blocks[0];
                double eaves = b.storeys * StoreyHeight;
                double rise = b.d / 2 * Math.Tan(RhinoMath.ToRadians(35));
                Brep roof = GableRoof(b.x, b.y, b.w, b.d, eaves, rise, s);
                roof.Transform(Transform.Rotation(RhinoMath.ToRadians(b.rot), Vector3d.ZAxis, new Point3d(b.x * s, b.y * s, 0)));
                scene.Buildings[0].Volumes.Add(roof);
            }

            // D: stepped block north of A. Storeys 1-3: 24 x 14 m. Storeys 4-5: west 16 m only.
            // Storey 6: west 16 m, south 9 m only. Upper storeys never cover the whole floor below.
            const double dx = 0, dy = 30;
            var d = new DemoBuilding { Label = "D" };
            for (int i = 0; i < 3; i++) d.Volumes.Add(Box(dx, dy, i * StoreyHeight, dx + 24, dy + 14, (i + 1) * StoreyHeight, s));
            for (int i = 3; i < 5; i++) d.Volumes.Add(Box(dx, dy, i * StoreyHeight, dx + 16, dy + 14, (i + 1) * StoreyHeight, s));
            d.Volumes.Add(Box(dx, dy, 5 * StoreyHeight, dx + 16, dy + 9, 6 * StoreyHeight, s));
            d.Plot = Rectangle(dx - PlotOffset, dy - PlotOffset, dx + 24 + PlotOffset, dy + 14 + PlotOffset, s, Transform.Identity);
            scene.Buildings.Add(d);
            return scene;
        }

        private static Curve Rectangle(double x0, double y0, double x1, double y1, double s, Transform xform)
        {
            var pl = new Polyline(new[]
            {
                new Point3d(x0 * s, y0 * s, 0), new Point3d(x1 * s, y0 * s, 0), new Point3d(x1 * s, y1 * s, 0),
                new Point3d(x0 * s, y1 * s, 0), new Point3d(x0 * s, y0 * s, 0),
            });
            pl.Transform(xform);
            return pl.ToPolylineCurve();
        }

        private static Brep Box(double x0, double y0, double z0, double x1, double y1, double z1, double s) =>
            Brep.CreateFromBox(new BoundingBox(x0 * s, y0 * s, z0 * s, x1 * s, y1 * s, z1 * s));

        /// <summary>Triangular roof prism on a w (x) by d (y) rectangle at height z, ridge along x.</summary>
        private static Brep GableRoof(double x, double y, double w, double d, double z, double rise, double s)
        {
            var corners = new[]
            {
                new Point3d(x, y, z), new Point3d(x + w, y, z), new Point3d(x + w, y + d / 2, z + rise), new Point3d(x, y + d / 2, z + rise),
                new Point3d(x, y + d, z), new Point3d(x + w, y + d, z),
            };
            for (int i = 0; i < corners.Length; i++) corners[i] *= s;
            Point3d p0 = corners[0], p1 = corners[1], r1 = corners[2], r0 = corners[3], q0 = corners[4], q1 = corners[5];

            var faces = new[]
            {
                Brep.CreateFromCornerPoints(p0, p1, r1, r0, 0.001 * s),   // south slope
                Brep.CreateFromCornerPoints(q1, q0, r0, r1, 0.001 * s),   // north slope
                Brep.CreateFromCornerPoints(p0, r0, q0, 0.001 * s),       // west gable
                Brep.CreateFromCornerPoints(p1, q1, r1, 0.001 * s),       // east gable
                Brep.CreateFromCornerPoints(p0, q0, q1, p1, 0.001 * s),   // bottom
            };
            Brep[] joined = Brep.JoinBreps(faces, 0.001 * s);
            Brep roof = joined[0];
            if (roof.SolidOrientation == BrepSolidOrientation.Inward) roof.Flip();
            return roof;
        }
    }
}
