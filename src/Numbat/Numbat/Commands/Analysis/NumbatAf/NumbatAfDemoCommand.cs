using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Numbat.Commands.Analysis.NumbatAf
{
    /// <summary>
    /// Demo: builds an example on an "nbAF demo" layer and registers the volumes with nbAF, so nbAF can be tried
    /// straight away. Two scenes: Site (default) is a district mass model of some 450 storey volumes and a ground
    /// surface (<see cref="AfDemoSite"/>); Blocks is four small test buildings with plots (<see cref="AfDemoScene"/>),
    /// whose results are listed in CLAUDE.md. Running it again replaces the layer's contents. Shipped in every build
    /// but hidden. Dimensions are in metres and converted to the model's units, so it works in mm and m files alike.
    /// </summary>
    // Hidden: works when typed in full, but doesn't appear in autocomplete, so typing "nb" only offers nbAF.
    [CommandStyle(Style.Hidden)]
    public class NumbatAfDemoCommand : Command
    {
        private const string LayerName = "nbAF demo";
        private const string GroundLayerName = "Ground";

        public override string EnglishName => "nbAFDemo";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            bool blocks = false;
            if (mode == RunMode.Interactive)
            {
                var go = new GetOption();
                go.SetCommandPrompt("Demo scene (Enter for the site)");
                go.AcceptNothing(true);
                var scene = new OptionToggle(false, "Site", "Blocks");
                go.AddOptionToggle("Scene", ref scene);
                while (true)
                {
                    GetResult r = go.Get();
                    if (r == GetResult.Option) continue;
                    if (r == GetResult.Nothing) break;
                    return Result.Cancel;
                }
                blocks = scene.CurrentValue;
            }

            int layer = GetOrAddLayer(doc, LayerName, Guid.Empty, Color.FromArgb(200, 200, 205));
            int groundLayer = layer < 0 ? -1 : GetOrAddLayer(doc, GroundLayerName, doc.Layers[layer].Id, Color.FromArgb(150, 175, 130));
            if (layer < 0 || groundLayer < 0)
            {
                RhinoApp.WriteLine($"{AfInfo.Name}: could not create the \"{LayerName}\" layers.");
                return Result.Failure;
            }
            foreach (int index in new[] { layer, groundLayer })
            {
                foreach (RhinoObject old in doc.Objects.FindByLayer(doc.Layers[index]) ?? Array.Empty<RhinoObject>())
                {
                    doc.Objects.Delete(old, true);
                }
            }

            double s = RhinoMath.UnitScale(UnitSystem.Meters, doc.ModelUnitSystem);
            string message = blocks ? AddBlocks(doc, layer, s) : AddSite(doc, layer, groundLayer, s);
            if (message == null) return Result.Failure;

            doc.Views.ActiveView?.ActiveViewport.ZoomExtents();
            doc.Views.Redraw();
            RhinoApp.WriteLine($"{AfInfo.Name}: {message} Run nbAF to see the Abstandsflächen.");
            return Result.Success;
        }

        private static ObjectAttributes Tracked(int layer)
        {
            var attr = new ObjectAttributes { LayerIndex = layer };
            attr.SetUserString(AfInfo.RoleKey, AfModel.Tracked);
            return attr;
        }

        private static string AddSite(RhinoDoc doc, int layer, int groundLayer, double s)
        {
            AfDemoSite site = AfDemoSite.Load();
            if (site == null)
            {
                RhinoApp.WriteLine($"{AfInfo.Name}: the demo site was not included in this build.");
                return null;
            }
            int count = 0;
            foreach (Extrusion e in site.Volumes(s))
            {
                if (doc.Objects.AddExtrusion(e, Tracked(layer)) != Guid.Empty) count++;
            }
            Brep ground = site.Ground(s, doc.ModelAbsoluteTolerance);
            if (ground != null) doc.Objects.AddBrep(ground, new ObjectAttributes { LayerIndex = groundLayer, Name = "Ground" });
            return $"added a site of {count} storey volumes on layer \"{LayerName}\", and its ground surface (not tracked) on " +
                   $"\"{LayerName}::{GroundLayerName}\". It has no plots.";
        }

        private static string AddBlocks(RhinoDoc doc, int layer, double s)
        {
            var scene = AfDemoScene.Build(s);
            int count = 0;
            foreach (AfDemoScene.DemoBuilding b in scene.Buildings)
            {
                Guid plot = doc.Objects.AddCurve(b.Plot, new ObjectAttributes { LayerIndex = layer, Name = $"Plot {b.Label}" });
                foreach (Brep v in b.Volumes)
                {
                    ObjectAttributes attr = Tracked(layer);
                    attr.Name = $"Block {b.Label}";
                    attr.SetUserString(AfInfo.PlotKey, plot.ToString());
                    if (doc.Objects.AddBrep(v, attr) != Guid.Empty) count++;
                }
            }
            return $"added {count} storey volumes (blocks A–D, each with a plot 4 m around it) on layer \"{LayerName}\".";
        }

        private static int GetOrAddLayer(RhinoDoc doc, string name, Guid parent, Color colour)
        {
            string path = parent == Guid.Empty ? name : $"{doc.Layers.FindId(parent).FullPath}::{name}";
            int existing = doc.Layers.FindByFullPath(path, -1);
            if (existing >= 0) return existing;
            return doc.Layers.Add(new Layer { Name = name, Color = colour, ParentLayerId = parent });
        }
    }

    /// <summary>
    /// The demo site: storey volumes as plan outlines with a base height and a height, plus a ground surface, read
    /// from AfDemoSite.txt (embedded). The file holds coordinates only, made from a real mass model that was
    /// mirrored and rotated so it matches no real place.
    /// </summary>
    internal sealed class AfDemoSite
    {
        private readonly List<(double z0, double height, Point3d[] outline)> _volumes = new List<(double, double, Point3d[])>();
        private Point3d[] _ground;

        public static AfDemoSite Load()
        {
            System.Reflection.Assembly asm = typeof(AfDemoSite).Assembly;
            string name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("AfDemoSite.txt"));
            if (name == null) return null;
            var site = new AfDemoSite();
            using (var reader = new System.IO.StreamReader(asm.GetManifestResourceStream(name)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0 || parts[0].StartsWith("#")) continue;
                    double[] n = parts.Skip(1).Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    if (parts[0] == "V" && n.Length >= 8) site._volumes.Add((n[0], n[1], Points(n, 2)));
                    else if (parts[0] == "G" && n.Length >= 6) site._ground = Points(n, 0);
                }
            }
            return site;
        }

        private static Point3d[] Points(double[] n, int start)
        {
            var pts = new List<Point3d>();
            for (int i = start; i + 1 < n.Length; i += 2) pts.Add(new Point3d(n[i], n[i + 1], 0));
            return pts.ToArray();
        }

        /// <param name="s">One metre in model units.</param>
        public IEnumerable<Extrusion> Volumes(double s)
        {
            foreach (var (z0, height, outline) in _volumes)
            {
                var pts = outline.Select(p => new Point3d(p.X * s, p.Y * s, z0 * s)).ToList();
                pts.Add(pts[0]);
                // Outlines run anticlockwise, so a positive height extrudes upwards.
                Extrusion e = Extrusion.Create(new PolylineCurve(pts), height * s, true);
                if (e != null) yield return e;
            }
        }

        public Brep Ground(double s, double tol)
        {
            if (_ground == null) return null;
            var pts = _ground.Select(p => new Point3d(p.X * s, p.Y * s, 0)).ToList();
            pts.Add(pts[0]);
            return Brep.CreatePlanarBreps(new PolylineCurve(pts), tol)?.FirstOrDefault();
        }
    }

    /// <summary>
    /// The Blocks demo scene, in the shape nbAF expects: one closed box per storey, stacked.
    /// Blocks A–C are nbGFADemo's three apartment blocks (2, 4 and 6 storeys of 3.2 m, slightly rotated).
    /// Block A also gets a 35° gable roof as a separate volume. Block D steps back: its upper storeys cover only
    /// part of the floor below. Each building's plot is its ground-floor footprint offset by 4 m.
    /// The expected results are listed in CLAUDE.md (nbAF).
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
                var building = new DemoBuilding { Label = ((char)('A' + k)).ToString() };
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
