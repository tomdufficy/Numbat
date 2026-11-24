using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Rhino;
using Rhino.Geometry;
using SdColor = System.Drawing.Color;

namespace Numbat.Commands.Analysis.NumbatAf
{
    /// <summary>What an export drawing is made of. Each kind has its own layer (DXF) or group (SVG) and style.</summary>
    internal enum ExportKind { Areas, Clashes, Footprints, Plots, Dimensions, Notes, Legend }

    /// <summary>What goes into an export besides the areas, footprints and plots. Remembered per user.</summary>
    internal sealed class ExportOptions
    {
        private const string Prefix = AfInfo.SettingsPrefix + "export.";

        public bool Clashes = true;      // red closed curves of the clash zones
        public bool Numbers = true;      // numbered markers on the clash zones
        public bool Notes = true;        // what each clash is, next to it (two lines)
        public bool Dimensions;          // depth dimensions, as in the viewport

        public static ExportOptions Load()
        {
            var s = NumbatPlugin.Instance.Settings;
            return new ExportOptions
            {
                Clashes = s.GetBool(Prefix + "clashes", true),
                Numbers = s.GetBool(Prefix + "numbers", true),
                Notes = s.GetBool(Prefix + "notes", true),
                Dimensions = s.GetBool(Prefix + "dimensions", false),
            };
        }

        public void Save()
        {
            var s = NumbatPlugin.Instance.Settings;
            s.SetBool(Prefix + "clashes", Clashes);
            s.SetBool(Prefix + "numbers", Numbers);
            s.SetBool(Prefix + "notes", Notes);
            s.SetBool(Prefix + "dimensions", Dimensions);
        }
    }

    /// <summary>One closed outline in plan, in metres.</summary>
    internal sealed class Shape
    {
        public ExportKind Kind;
        public List<Point2d> Points;
    }

    /// <summary>A line of text in plan, in metres: centred on <see cref="At"/>, rotated by <see cref="Angle"/> degrees.</summary>
    internal sealed class Text2d
    {
        public ExportKind Kind;
        public Point2d At;
        public string Text;
        public double Height;
        public double Angle;
    }

    /// <summary>A depth dimension: the line, its ticks are drawn by each writer.</summary>
    internal sealed class Dim2d
    {
        public Point2d A, B;
    }

    /// <summary>A numbered clash marker: a red disc with the number in white.</summary>
    internal sealed class Marker2d
    {
        public Point2d At;
        public string Number;
    }

    /// <summary>Everything an export contains, in metres, top view.</summary>
    internal sealed class ExportData
    {
        public readonly List<Shape> Shapes = new List<Shape>();
        public readonly List<Text2d> Texts = new List<Text2d>();
        public readonly List<Dim2d> Dims = new List<Dim2d>();
        public readonly List<Marker2d> Markers = new List<Marker2d>();
        public readonly List<(ExportKind kind, string text)> Legend = new List<(ExportKind, string)>();
        public readonly List<string> Notes = new List<string>();
        public BoundingBox Bounds = BoundingBox.Empty;
        public double Base;          // text size the drawing is built around, in metres
        public double MarkerRadius => Base * 0.55;
    }

    /// <summary>
    /// Exports the Abstandsflächen as a 2D top view in metres: DXF (R12, one layer per kind), SVG (1:500) and JPG.
    /// A drawing convention independent of the viewport colours: every Abstandsfläche is a closed light blue curve
    /// (pale fill in SVG and JPG), clash zones are separate closed red curves, plots are blue dashed lines and
    /// footprints are black outlines filled white. Clash numbers, clash notes and depth dimensions are optional.
    /// A legend lists the elements, the rule values, the source and the copyright line.
    /// </summary>
    internal static class AfExporter
    {
        public const double SvgScale = 500; // 1:500

        private static readonly Dictionary<ExportKind, (SdColor stroke, SdColor fill, int aci)> Style = new Dictionary<ExportKind, (SdColor, SdColor, int)>
        {
            [ExportKind.Areas] = (SdColor.FromArgb(91, 166, 222), SdColor.FromArgb(226, 240, 251), 151),
            [ExportKind.Clashes] = (SdColor.FromArgb(214, 40, 40), SdColor.FromArgb(248, 205, 205), 1),
            [ExportKind.Footprints] = (SdColor.FromArgb(0, 0, 0), SdColor.FromArgb(255, 255, 255), 7),
            [ExportKind.Plots] = (SdColor.FromArgb(31, 95, 191), SdColor.Empty, 5),
            [ExportKind.Dimensions] = (SdColor.FromArgb(60, 60, 60), SdColor.Empty, 8),
            [ExportKind.Notes] = (SdColor.FromArgb(34, 34, 34), SdColor.Empty, 7),
            [ExportKind.Legend] = (SdColor.FromArgb(34, 34, 34), SdColor.Empty, 7),
        };

        private static string LayerName(ExportKind k) => "nbAF-" + k;

        public static ExportData Collect(AfResult a, AfRules rules, ExportOptions o, RhinoDoc doc)
        {
            double metre = RhinoMath.UnitScale(UnitSystem.Meters, doc.ModelUnitSystem);
            double tol = doc.ModelAbsoluteTolerance;
            var d = new ExportData();
            Point2d P(Point3d p) => new Point2d(p.X / metre, p.Y / metre);

            void Add(ExportKind k, Curve c)
            {
                List<Point2d> pts = Points(c, tol, metre);
                if (pts.Count < 3) return;
                d.Shapes.Add(new Shape { Kind = k, Points = pts });
                foreach (Point2d p in pts) d.Bounds.Union(new Point3d(p.X, p.Y, 0));
            }

            foreach (AfFace f in a.Faces)
            {
                foreach (Curve c in f.Regions) Add(ExportKind.Areas, c);
            }
            if (o.Clashes)
            {
                foreach (Flag f in a.Flags) Add(ExportKind.Clashes, f.Region);
            }
            for (int i = 0; i < a.Buildings.Count; i++)
            {
                foreach (Curve c in a.Footprints[i]) Add(ExportKind.Footprints, c);
                if (a.Buildings[i].Plot != null) Add(ExportKind.Plots, a.Buildings[i].Plot);
            }

            // Text is sized to the drawing: about 1/110 of its extent, between 0.4 and 1.5 m (0.8–3 mm at 1:500).
            double extent = Math.Max(d.Bounds.Max.X - d.Bounds.Min.X, d.Bounds.Max.Y - d.Bounds.Min.Y);
            d.Base = Math.Max(0.4, Math.Min(1.5, extent / 110));
            double small = d.Base * 0.7;

            for (int i = 0; i < a.Buildings.Count; i++)
            {
                BuildingInput b = a.Buildings[i];
                if (a.Footprints[i].Count == 0) continue;
                Point3d centre = AreaMassProperties.Compute(a.Footprints[i][0])?.Centroid ?? Point3d.Origin;
                d.Texts.Add(new Text2d { Kind = ExportKind.Notes, At = P(centre), Height = small, Text = b.Name });
            }

            if (o.Dimensions)
            {
                foreach (AfLabel l in a.Labels)
                {
                    d.Dims.Add(new Dim2d { A = P(l.Start), B = P(l.End) });
                    Vector3d x = l.Plane.XAxis;
                    Point3d mid = (l.Start + l.End) / 2 + l.Plane.YAxis * (small * metre);
                    d.Texts.Add(new Text2d
                    {
                        Kind = ExportKind.Dimensions, At = P(mid), Height = small * 0.85, Text = l.Text,
                        Angle = RhinoMath.ToDegrees(Math.Atan2(x.Y, x.X)),
                    });
                }
            }

            if (o.Clashes)
            {
                foreach (Flag f in a.Flags)
                {
                    Point2d at = P(f.DotAt);
                    if (o.Numbers) d.Markers.Add(new Marker2d { At = at, Number = f.Number.ToString() });
                    if (!o.Notes) continue;
                    // Two short lines under the marker (or on the zone without one): what it is, and how big.
                    double y = at.Y - (o.Numbers ? d.MarkerRadius + small * 0.9 : 0);
                    d.Texts.Add(new Text2d { Kind = ExportKind.Notes, At = new Point2d(at.X, y), Height = small, Text = f.Text });
                    d.Texts.Add(new Text2d
                    {
                        Kind = ExportKind.Notes, At = new Point2d(at.X, y - small * 1.25), Height = small,
                        Text = $"{f.Area.ToString("0.0", CultureInfo.InvariantCulture)} m²",
                    });
                }
            }

            d.Legend.Add((ExportKind.Areas, "Abstandsflächen"));
            if (o.Clashes) d.Legend.Add((ExportKind.Clashes, $"Clashes ({a.Flags.Count})"));
            d.Legend.Add((ExportKind.Plots, "Plot lines"));
            d.Legend.Add((ExportKind.Footprints, "Building footprints"));

            d.Notes.Add($"Rules: {rules.Describe()}. Roofs under {AfRules.N(rules.SteepRoofDeg, "0.#")}° add " +
                        $"{AfRules.Field(AfRules.RoofShareKey).Format(rules.ShallowRoofShare)} of their height, steeper roofs all of it.");
            if (!rules.IsDefault) d.Notes.Add("Values differ from the HBauO defaults (0.4 H, 0.2 H in GE/GI, at least 2.50 m, 70°, 1/3, 75°).");
            d.Notes.Add("Based on the Hamburgische Bauordnung (HBauO) § 6, as of 25.09.2025. Only Abstandsflächen reaching another plot are " +
                        "checked; the half-road rule and Baulasten are not considered.");
            if (o.Clashes && o.Numbers)
            {
                foreach (Flag f in a.Flags) d.Notes.Add($"Clash {f.Number}: {f.Describe()}");
            }
            d.Notes.Add($"Made with Numbat {AfInfo.Name}, {DateTime.Now:yyyy-MM-dd}. {AfInfo.Copyright}");
            return d;
        }

        private static List<Point2d> Points(Curve c, double tol, double metre)
        {
            Polyline pl;
            if (!c.TryGetPolyline(out pl))
            {
                PolylineCurve approx = c.ToPolyline(tol * 10, RhinoMath.ToRadians(5), 0, 0);
                if (approx == null || !approx.TryGetPolyline(out pl)) return new List<Point2d>();
            }
            var pts = pl.Select(p => new Point2d(p.X / metre, p.Y / metre)).ToList();
            if (pts.Count > 1 && pts[0].DistanceTo(pts[pts.Count - 1]) < 1e-6) pts.RemoveAt(pts.Count - 1); // closed: drop repeat
            return pts;
        }

        /// <summary>The two short oblique ticks at the ends of a dimension line.</summary>
        private static IEnumerable<(Point2d a, Point2d b)> Ticks(Dim2d dim, double size)
        {
            Vector2d along = dim.B - dim.A;
            along.Unitize();
            var tick = new Vector2d(along.X - along.Y, along.Y + along.X);
            tick.Unitize();
            tick *= size;
            yield return (dim.A - tick, dim.A + tick);
            yield return (dim.B - tick, dim.B + tick);
        }

        // Legend and notes under the drawing.
        private static double LegendHeight(ExportData d) => (d.Legend.Count * 1.8 + d.Notes.Count * 1.4 + 2) * d.Base;

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Hex(SdColor c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        // Draw order: areas, clashes, footprints (white fill), plots on top.
        private static readonly ExportKind[] ShapeOrder = { ExportKind.Areas, ExportKind.Clashes, ExportKind.Footprints, ExportKind.Plots };

        // ---- DXF (R12) -------------------------------------------------------------------

        public static void WriteDxf(string path, ExportData d)
        {
            var sb = new StringBuilder();
            void G(int code, string value) => sb.Append(code).Append("\r\n").Append(value).Append("\r\n");
            string I(int v) => v.ToString(CultureInfo.InvariantCulture);

            G(0, "SECTION"); G(2, "HEADER");
            G(9, "$ACADVER"); G(1, "AC1009");
            G(9, "$INSUNITS"); G(70, "6"); // metres
            G(0, "ENDSEC");

            G(0, "SECTION"); G(2, "TABLES");
            G(0, "TABLE"); G(2, "LTYPE"); G(70, "2");
            G(0, "LTYPE"); G(2, "CONTINUOUS"); G(70, "0"); G(3, "Solid line"); G(72, "65"); G(73, "0"); G(40, "0.0");
            G(0, "LTYPE"); G(2, "DASHED"); G(70, "0"); G(3, "Dashed __ __ __"); G(72, "65"); G(73, "2"); G(40, "1.5"); G(49, "1.0"); G(49, "-0.5");
            G(0, "ENDTAB");
            var kinds = (ExportKind[])Enum.GetValues(typeof(ExportKind));
            G(0, "TABLE"); G(2, "LAYER"); G(70, I(kinds.Length));
            foreach (ExportKind k in kinds)
            {
                G(0, "LAYER"); G(2, LayerName(k)); G(70, "0"); G(62, I(Style[k].aci)); G(6, k == ExportKind.Plots ? "DASHED" : "CONTINUOUS");
            }
            G(0, "ENDTAB");
            G(0, "ENDSEC");

            G(0, "SECTION"); G(2, "ENTITIES");
            void Poly(string layer, IEnumerable<(double x, double y)> pts, bool closed)
            {
                G(0, "POLYLINE"); G(8, layer); G(66, "1"); G(70, closed ? "1" : "0"); G(10, "0"); G(20, "0"); G(30, "0");
                foreach (var (x, y) in pts)
                {
                    G(0, "VERTEX"); G(8, layer); G(10, F(x)); G(20, F(y)); G(30, "0");
                }
                G(0, "SEQEND"); G(8, layer);
            }
            void Line(string layer, Point2d a, Point2d b)
            {
                G(0, "LINE"); G(8, layer); G(10, F(a.X)); G(20, F(a.Y)); G(30, "0"); G(11, F(b.X)); G(21, F(b.Y)); G(31, "0");
            }
            // Text centred on a point (R12: horizontal 1 = centre, vertical 2 = middle, aligned on 11/21).
            void Text(string layer, double x, double y, double size, string text, double angle = 0, bool centred = true)
            {
                G(0, "TEXT"); G(8, layer); G(10, F(x)); G(20, F(y)); G(30, "0"); G(40, F(size)); G(1, DxfText(text));
                if (Math.Abs(angle) > 1e-6) G(50, F(angle));
                if (centred) { G(72, "1"); G(73, "2"); G(11, F(x)); G(21, F(y)); G(31, "0"); }
            }

            foreach (ExportKind k in ShapeOrder)
            {
                foreach (Shape s in d.Shapes.Where(s => s.Kind == k)) Poly(LayerName(k), s.Points.Select(p => (p.X, p.Y)), true);
            }
            foreach (Dim2d dim in d.Dims)
            {
                Line(LayerName(ExportKind.Dimensions), dim.A, dim.B);
                foreach (var t in Ticks(dim, d.Base * 0.25)) Line(LayerName(ExportKind.Dimensions), t.a, t.b);
            }
            foreach (Marker2d m in d.Markers)
            {
                G(0, "CIRCLE"); G(8, LayerName(ExportKind.Clashes)); G(10, F(m.At.X)); G(20, F(m.At.Y)); G(30, "0"); G(40, F(d.MarkerRadius));
                Text(LayerName(ExportKind.Clashes), m.At.X, m.At.Y, d.MarkerRadius, m.Number);
            }
            foreach (Text2d t in d.Texts) Text(LayerName(t.Kind), t.At.X, t.At.Y, t.Height, t.Text, t.Angle);

            double h = d.Base;
            double y0 = d.Bounds.Min.Y - h * 3;
            foreach (var (kind, text) in d.Legend)
            {
                double x = d.Bounds.Min.X;
                Poly(LayerName(kind), new[] { (x, y0), (x + h * 2, y0), (x + h * 2, y0 + h), (x, y0 + h) }, kind != ExportKind.Plots);
                Text(LayerName(ExportKind.Legend), x + h * 3, y0, h, text, 0, false);
                y0 -= h * 1.8;
            }
            foreach (string note in d.Notes)
            {
                Text(LayerName(ExportKind.Legend), d.Bounds.Min.X, y0, h * 0.7, note, 0, false);
                y0 -= h * 1.4;
            }
            G(0, "ENDSEC");
            G(0, "EOF");
            File.WriteAllText(path, sb.ToString(), Encoding.ASCII);
        }

        /// <summary>DXF R12 text is 8-bit: write other characters as \U+XXXX, which CAD programs understand.</summary>
        private static string DxfText(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c >= 32 && c < 127) sb.Append(c);
                else sb.Append("\\U+").Append(((int)c).ToString("X4"));
            }
            return sb.ToString();
        }

        // ---- SVG (1:500) -----------------------------------------------------------------

        public static void WriteSvg(string path, ExportData d)
        {
            double h = d.Base;
            double margin = h * 2;
            double minX = d.Bounds.Min.X - margin, maxY = d.Bounds.Max.Y + margin;
            double w = d.Bounds.Max.X - d.Bounds.Min.X + 2 * margin;
            double ht = d.Bounds.Max.Y - d.Bounds.Min.Y + 2 * margin + LegendHeight(d);
            string X(double px) => F(px - minX);
            string Y(double py) => F(maxY - py);
            double mm = 1000 / SvgScale;
            string font = "font-family=\"Arial, Helvetica, sans-serif\"";

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(w * mm)}mm\" height=\"{F(ht * mm)}mm\" viewBox=\"0 0 {F(w)} {F(ht)}\">");
            sb.AppendLine($"<title>{AfInfo.Title}, top view, 1:{SvgScale:0} (units: m)</title>");
            sb.AppendLine($"<rect width=\"{F(w)}\" height=\"{F(ht)}\" fill=\"#ffffff\"/>");

            foreach (ExportKind k in ShapeOrder)
            {
                var shapes = d.Shapes.Where(s => s.Kind == k).ToList();
                if (shapes.Count == 0) continue;
                var st = Style[k];
                string fill = st.fill.IsEmpty ? "none" : Hex(st.fill);
                string dash = k == ExportKind.Plots ? $" stroke-dasharray=\"{F(h * 0.8)} {F(h * 0.4)}\"" : "";
                double width = k == ExportKind.Footprints ? h * 0.07 : h * 0.06;
                sb.AppendLine($"<g id=\"{k}\" stroke=\"{Hex(st.stroke)}\" stroke-width=\"{F(width)}\" stroke-linejoin=\"round\" fill=\"{fill}\"{dash}>");
                foreach (Shape s in shapes)
                {
                    sb.AppendLine($"  <polygon points=\"{string.Join(" ", s.Points.Select(p => $"{X(p.X)},{Y(p.Y)}"))}\"/>");
                }
                sb.AppendLine("</g>");
            }

            if (d.Dims.Count > 0)
            {
                sb.AppendLine($"<g id=\"Dimensions\" stroke=\"{Hex(Style[ExportKind.Dimensions].stroke)}\" stroke-width=\"{F(h * 0.03)}\">");
                foreach (Dim2d dim in d.Dims)
                {
                    sb.AppendLine($"  <line x1=\"{X(dim.A.X)}\" y1=\"{Y(dim.A.Y)}\" x2=\"{X(dim.B.X)}\" y2=\"{Y(dim.B.Y)}\"/>");
                    foreach (var t in Ticks(dim, h * 0.25))
                        sb.AppendLine($"  <line x1=\"{X(t.a.X)}\" y1=\"{Y(t.a.Y)}\" x2=\"{X(t.b.X)}\" y2=\"{Y(t.b.Y)}\" stroke-width=\"{F(h * 0.06)}\"/>");
                }
                sb.AppendLine("</g>");
            }

            if (d.Markers.Count > 0)
            {
                string red = Hex(Style[ExportKind.Clashes].stroke);
                sb.AppendLine($"<g id=\"Markers\" {font} font-size=\"{F(d.MarkerRadius)}\" font-weight=\"bold\" text-anchor=\"middle\" dominant-baseline=\"central\">");
                foreach (Marker2d m in d.Markers)
                {
                    sb.AppendLine($"  <circle cx=\"{X(m.At.X)}\" cy=\"{Y(m.At.Y)}\" r=\"{F(d.MarkerRadius)}\" fill=\"{red}\"/>");
                    sb.AppendLine($"  <text x=\"{X(m.At.X)}\" y=\"{Y(m.At.Y)}\" fill=\"#ffffff\">{Esc(m.Number)}</text>");
                }
                sb.AppendLine("</g>");
            }

            sb.AppendLine($"<g id=\"Text\" {font} text-anchor=\"middle\" dominant-baseline=\"central\">");
            foreach (Text2d t in d.Texts)
            {
                string rotate = Math.Abs(t.Angle) > 1e-6 ? $" transform=\"rotate({F(-t.Angle)} {X(t.At.X)} {Y(t.At.Y)})\"" : "";
                sb.AppendLine($"  <text x=\"{X(t.At.X)}\" y=\"{Y(t.At.Y)}\" font-size=\"{F(t.Height)}\" fill=\"{Hex(Style[t.Kind].stroke)}\"{rotate}>{Esc(t.Text)}</text>");
            }
            sb.AppendLine("</g>");

            sb.AppendLine($"<g id=\"Legend\" {font} font-size=\"{F(h)}\" fill=\"#222222\">");
            double y = d.Bounds.Min.Y - margin - h * 1.5;
            foreach (var (kind, text) in d.Legend)
            {
                var st = Style[kind];
                string fill = st.fill.IsEmpty ? "none" : Hex(st.fill);
                string dash = kind == ExportKind.Plots ? $" stroke-dasharray=\"{F(h * 0.5)} {F(h * 0.25)}\"" : "";
                sb.AppendLine($"  <rect x=\"{X(d.Bounds.Min.X)}\" y=\"{Y(y + h)}\" width=\"{F(h * 2)}\" height=\"{F(h)}\" fill=\"{fill}\" stroke=\"{Hex(st.stroke)}\" stroke-width=\"{F(h * 0.07)}\"{dash}/>");
                sb.AppendLine($"  <text x=\"{X(d.Bounds.Min.X + h * 3)}\" y=\"{Y(y)}\">{Esc(text)}</text>");
                y -= h * 1.8;
            }
            foreach (string note in d.Notes)
            {
                sb.AppendLine($"  <text x=\"{X(d.Bounds.Min.X)}\" y=\"{Y(y)}\" font-size=\"{F(h * 0.7)}\" fill=\"#555555\">{Esc(note)}</text>");
                y -= h * 1.4;
            }
            sb.AppendLine("</g>");
            sb.AppendLine("</svg>");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        // ---- JPG -------------------------------------------------------------------------

        public static void WriteJpg(string path, ExportData d, int width = 2400)
        {
            double h = d.Base;
            double margin = h * 2;
            double w = d.Bounds.Max.X - d.Bounds.Min.X + 2 * margin;
            double ht = d.Bounds.Max.Y - d.Bounds.Min.Y + 2 * margin + LegendHeight(d);
            float s = (float)(width / w);
            int height = (int)Math.Ceiling(ht * s);
            double minX = d.Bounds.Min.X - margin, maxY = d.Bounds.Max.Y + margin;
            Eto.Drawing.PointF P(double px, double py) => new Eto.Drawing.PointF((float)((px - minX) * s), (float)((maxY - py) * s));
            Eto.Drawing.Color E(SdColor c) => Eto.Drawing.Color.FromArgb(c.R, c.G, c.B);
            Eto.Drawing.Font Font(double metres, bool bold = false) =>
                new Eto.Drawing.Font(bold ? Eto.Drawing.SystemFont.Bold : Eto.Drawing.SystemFont.Default, Math.Max(1f, (float)(metres * s * 0.75)));

            using (var bmp = new Eto.Drawing.Bitmap(width, height, Eto.Drawing.PixelFormat.Format32bppRgb))
            {
                using (var g = new Eto.Drawing.Graphics(bmp))
                {
                    g.AntiAlias = true;
                    g.FillRectangle(Eto.Drawing.Colors.White, 0, 0, width, height);

                    foreach (ExportKind k in ShapeOrder)
                    {
                        var st = Style[k];
                        var pen = new Eto.Drawing.Pen(E(st.stroke), Math.Max(1.5f, (float)(h * 0.06 * s)));
                        if (k == ExportKind.Plots) pen.DashStyle = new Eto.Drawing.DashStyle(0, 4, 2);
                        foreach (Shape sh in d.Shapes.Where(x => x.Kind == k))
                        {
                            Eto.Drawing.PointF[] pts = sh.Points.Select(p => P(p.X, p.Y)).ToArray();
                            if (!st.fill.IsEmpty) g.FillPolygon(E(st.fill), pts);
                            g.DrawPolygon(pen, pts);
                        }
                    }

                    var dimPen = new Eto.Drawing.Pen(E(Style[ExportKind.Dimensions].stroke), Math.Max(1f, (float)(h * 0.03 * s)));
                    var tickPen = new Eto.Drawing.Pen(E(Style[ExportKind.Dimensions].stroke), Math.Max(1.5f, (float)(h * 0.06 * s)));
                    foreach (Dim2d dim in d.Dims)
                    {
                        g.DrawLine(dimPen, P(dim.A.X, dim.A.Y), P(dim.B.X, dim.B.Y));
                        foreach (var t in Ticks(dim, h * 0.25)) g.DrawLine(tickPen, P(t.a.X, t.a.Y), P(t.b.X, t.b.Y));
                    }

                    void Centred(Eto.Drawing.Font font, Eto.Drawing.Color colour, Point2d at, string text, double angle)
                    {
                        Eto.Drawing.SizeF size = g.MeasureString(font, text);
                        Eto.Drawing.PointF c = P(at.X, at.Y);
                        g.SaveTransform();
                        g.TranslateTransform(c.X, c.Y);
                        if (Math.Abs(angle) > 1e-6) g.RotateTransform((float)-angle);
                        g.DrawText(font, colour, -size.Width / 2, -size.Height / 2, text);
                        g.RestoreTransform();
                    }

                    Eto.Drawing.Color red = E(Style[ExportKind.Clashes].stroke);
                    foreach (Marker2d m in d.Markers)
                    {
                        Eto.Drawing.PointF c = P(m.At.X, m.At.Y);
                        float r = (float)(d.MarkerRadius * s);
                        g.FillEllipse(red, c.X - r, c.Y - r, 2 * r, 2 * r);
                        Centred(Font(d.MarkerRadius, true), Eto.Drawing.Colors.White, m.At, m.Number, 0);
                    }
                    foreach (Text2d t in d.Texts) Centred(Font(t.Height), E(Style[t.Kind].stroke), t.At, t.Text, t.Angle);

                    var legendFont = Font(h);
                    double y = d.Bounds.Min.Y - margin - h * 1.5;
                    foreach (var (kind, text) in d.Legend)
                    {
                        var st = Style[kind];
                        Eto.Drawing.PointF a = P(d.Bounds.Min.X, y + h);
                        var box = new Eto.Drawing.RectangleF(a.X, a.Y, (float)(h * 2 * s), (float)(h * s));
                        if (!st.fill.IsEmpty) g.FillRectangle(E(st.fill), box);
                        var pen = new Eto.Drawing.Pen(E(st.stroke), 2);
                        if (kind == ExportKind.Plots) pen.DashStyle = new Eto.Drawing.DashStyle(0, 4, 2);
                        g.DrawRectangle(pen, box);
                        Eto.Drawing.PointF t = P(d.Bounds.Min.X + h * 3, y + h);
                        g.DrawText(legendFont, Eto.Drawing.Color.FromArgb(34, 34, 34), t.X, t.Y, text);
                        y -= h * 1.8;
                    }
                    var noteFont = Font(h * 0.7);
                    foreach (string note in d.Notes)
                    {
                        Eto.Drawing.PointF t = P(d.Bounds.Min.X, y + h);
                        g.DrawText(noteFont, Eto.Drawing.Color.FromArgb(85, 85, 85), t.X, t.Y, note);
                        y -= h * 1.4;
                    }
                }
                bmp.Save(path, Eto.Drawing.ImageFormat.Jpeg);
            }
        }
    }
}
