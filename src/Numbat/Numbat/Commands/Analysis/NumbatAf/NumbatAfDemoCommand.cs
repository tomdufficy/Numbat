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
    /// Demo: adds an example district of some 450 storey volumes (<see cref="AfDemoSite"/>) on an "nbAF demo" layer,
    /// as plain geometry, the same as a user's own model. nbAF doesn't track it until the user adds it in the
    /// window, so nbAF can be tried from the start. Running it again replaces the layer's contents. Shipped in every
    /// build but hidden. Dimensions are in metres and converted to the model's units, so it works in mm and m files.
    /// </summary>
    // Hidden: works when typed in full, but doesn't appear in autocomplete, so typing "nb" only offers nbAF.
    [CommandStyle(Style.Hidden)]
    public class NumbatAfDemoCommand : Command
    {
        private const string LayerName = "nbAF demo";

        public override string EnglishName => "nbAFDemo";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            AfDemoSite site = AfDemoSite.Load();
            if (site == null)
            {
                RhinoApp.WriteLine($"{AfInfo.Name}: the demo site was not included in this build.");
                return Result.Failure;
            }

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

            double s = RhinoMath.UnitScale(UnitSystem.Meters, doc.ModelUnitSystem);
            var attr = new ObjectAttributes { LayerIndex = layer };
            int count = 0;
            foreach (Extrusion e in site.Volumes(s))
            {
                if (doc.Objects.AddExtrusion(e, attr) != Guid.Empty) count++;
            }

            doc.Views.ActiveView?.ActiveViewport.ZoomExtents();
            doc.Views.Redraw();
            RhinoApp.WriteLine($"{AfInfo.Name}: added {count} storey volumes on layer \"{LayerName}\". To try nbAF, run nbAF, " +
                               "select the volumes and click Add.");
            return Result.Success;
        }

        private static int GetOrAddLayer(RhinoDoc doc)
        {
            int existing = doc.Layers.FindByFullPath(LayerName, -1);
            if (existing >= 0) return existing;
            return doc.Layers.Add(new Layer { Name = LayerName, Color = Color.FromArgb(200, 200, 205) });
        }
    }

    /// <summary>
    /// The demo site: storey volumes as plan outlines with a base height and a height, read
    /// from AfDemoSite.txt (embedded). The file holds coordinates only, from a hand-made mass model moved to the
    /// origin, with its small modelling gaps and overlaps kept as a test of the model check.
    /// </summary>
    internal sealed class AfDemoSite
    {
        private readonly List<(double z0, double height, Point3d[] outline)> _volumes = new List<(double, double, Point3d[])>();

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
    }
}
