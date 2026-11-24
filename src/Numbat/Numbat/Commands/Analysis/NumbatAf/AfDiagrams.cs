using System;
using System.Collections.Generic;
using System.Globalization;
using Eto.Drawing;
using Eto.Forms;
using Numbat.UI;

namespace Numbat.Commands.Analysis.NumbatAf
{
    /// <summary>
    /// The diagrams in nbAF's guide (docs/nbAF.md): the basic principles of Abstandsflächen, drawn in code with the
    /// kit's colours so they follow Rhino's light and dark mode. The numbers come from the HBauO defaults in
    /// <see cref="AfRules"/>. The PNGs next to the guide (docs/images) are exported from the same code
    /// (<see cref="SavePng"/>) for viewers such as GitHub; export them again after changing a diagram.
    /// Sections are drawn with the ground as a horizontal line; plans from above.
    /// </summary>
    internal static class AfDiagrams
    {
        private const float W = KitHelpWindow.TextWidth;

        private static readonly AfRules Rules = new AfRules();

        private static readonly Dictionary<string, (float height, Action<Graphics> paint)> All =
            new Dictionary<string, (float, Action<Graphics>)>
            {
                ["nbAF-height"] = (150, Height),
                ["nbAF-depth"] = (176, Depth),
                ["nbAF-roof"] = (186, Roof),
                ["nbAF-gable"] = (180, Gable),
                ["nbAF-steps"] = (262, Steps),
                ["nbAF-where"] = (214, Where),
            };

        /// <summary>The names of every diagram, as used in the guide ("nbAF-height", ...).</summary>
        public static IEnumerable<string> Names => All.Keys;

        /// <summary>A control showing the diagram, or null if there is none with that name (for the guide window).</summary>
        public static Control Create(string name) => All.ContainsKey(name) ? new View(name) : null;

        /// <summary>Saves a diagram as a PNG in the current colours, at <paramref name="scale"/> times its size.</summary>
        public static void SavePng(string name, string path, float scale = 2)
        {
            var (height, paint) = All[name];
            using (var bmp = new Bitmap((int)Math.Ceiling(W * scale), (int)Math.Ceiling(height * scale), PixelFormat.Format32bppRgba))
            {
                using (var g = new Graphics(bmp))
                {
                    g.AntiAlias = true;
                    g.ScaleTransform(scale);
                    g.FillRectangle(Kit.Colours.Surface, 0, 0, W, height);
                    paint(g);
                }
                bmp.Save(path, ImageFormat.Png);
            }
        }

        private sealed class View : Drawable
        {
            public View(string name)
            {
                var (height, paint) = All[name];
                Size = new Size((int)Math.Ceiling(W * Kit.Scale), (int)Math.Ceiling(height * Kit.Scale));
                Paint += (s, e) =>
                {
                    e.Graphics.AntiAlias = true;
                    e.Graphics.FillRectangle(Kit.Colours.Surface, 0, 0, Width, Height);
                    e.Graphics.ScaleTransform(Kit.Scale);
                    paint(e.Graphics);
                };
            }
        }

        // ---- the diagrams ----------------------------------------------------------------------

        /// <summary>What H is: from the ground to the top of the wall, or to where the wall meets the roof.</summary>
        private static void Height(Graphics g)
        {
            const float s = 6, ground = 106;
            Ground(g, 10, W - 10, ground);

            // Flat roof.
            Box(g, 34, ground - 12 * s, 15 * s, 12 * s);
            DimV(g, 34 + 15 * s + 12, ground, ground - 12 * s, "H", strong: true);
            Note(g, 24, ground + 12, 160, "Flat roof: H runs to the top of the wall.");

            // Pitched roof: H to the eaves, the roof comes on top (see the roof diagram).
            float x = 218, wall = 9 * s, half = 7.5f * s;
            Shape(g, new[]
            {
                new PointF(x, ground), new PointF(x, ground - wall), new PointF(x + half, ground - wall - 6 * s),
                new PointF(x + 2 * half, ground - wall), new PointF(x + 2 * half, ground),
            });
            DimV(g, x + 2 * half + 12, ground, ground - wall, "H", strong: true);
            Note(g, 208, ground + 12, 160, "Pitched roof: H runs to the eaves; part of the roof is added.");
        }

        /// <summary>Depth = factor × H, but never less than the minimum.</summary>
        private static void Depth(Graphics g)
        {
            const float s = 6, ground = 102;
            Ground(g, 10, W - 10, ground);
            DepthExample(g, 30, ground, s, 15);
            DepthExample(g, 214, ground, s, 4);
        }

        private static void DepthExample(Graphics g, float x, float ground, float s, double h)
        {
            float wall = (float)h * s;
            double raw = Rules.GeneralFactor * h;
            double depth = Math.Max(raw, Rules.MinDepthM);
            Box(g, x, ground - wall, 8 * s, wall);
            float face = x + 8 * s;
            AreaBand(g, face, ground, (float)depth * s);
            DimV(g, face + 8, ground, ground - wall, $"H = {M(h, 0)}");
            DimH(g, face, face + (float)depth * s, ground + 14, M(depth), below: true);
            string sum = $"{F(Rules.GeneralFactor)} × {M(h, 0)} = {M(raw)}";
            Note(g, x - 10, ground + 40, Math.Min(170, W - (x - 10) - 8),
                raw >= Rules.MinDepthM ? $"Depth: {sum}" : $"{sum}, less than the minimum, so the depth is {M(Rules.MinDepthM)}.");
        }

        /// <summary>Roofs flatter than the limit add a share of their height to H; steeper roofs all of it.</summary>
        private static void Roof(Graphics g)
        {
            const float s = 5, ground = 124;
            Ground(g, 10, W - 10, ground);
            RoofExample(g, 30, ground, s, 14, 45);
            RoofExample(g, 236, ground, s, 6, 75);
        }

        private static void RoofExample(Graphics g, float x, float ground, float s, double width, double pitch)
        {
            const double wallM = 9;
            double roofM = width / 2 * Math.Tan(pitch * Math.PI / 180);
            double share = Rules.RoofShare(pitch);
            double h = wallM + share * roofM;
            float wall = (float)wallM * s, roof = (float)roofM * s, w = (float)width * s;
            Shape(g, new[]
            {
                new PointF(x, ground), new PointF(x, ground - wall), new PointF(x + w / 2, ground - wall - roof),
                new PointF(x + w, ground - wall), new PointF(x + w, ground),
            });
            float d = x + w + 10;
            DimV(g, d, ground, ground - wall, M(wallM, 0));
            DashH(g, x + w / 2, d + 6, ground - wall - roof);
            DimV(g, d, ground - wall, ground - wall - roof, M(roofM, 1));
            // The pitch, inside a wide roof or beside a narrow one.
            string deg = $"{pitch:0}°";
            float degW = KitPaint.Measure(KitFonts.Caption, deg).Width;
            float degX = w > 2 * degW + 24 ? x + w / 2 - degW / 2 : x - degW - 6;
            KitPaint.TextMiddle(g, KitFonts.Caption, Kit.Colours.Text2, degX, ground - wall - (w > 2 * degW + 24 ? 18 : roof / 2 + 7), 14, deg);
            string rule = pitch < Rules.SteepRoofDeg
                ? $"Under {Rules.SteepRoofDeg:0}°, {Share(share)} of the roof counts.\nH = {M(wallM, 0)} + {Share(share)} × {M(roofM, 1)} = {M(h)}"
                : $"{Rules.SteepRoofDeg:0}° or steeper, all of it counts.\nH = {M(wallM, 0)} + {M(roofM, 1)} = {M(h)}";
            Note(g, x - 20, ground + 10, Math.Min(178, W - (x - 20) - 6), rule);
        }

        /// <summary>A gable wall counts up to the ridge, so its area follows the shape of the gable.</summary>
        private static void Gable(Graphics g)
        {
            const float s = 5, ground = 92, x = 130;
            const double wallM = 9, roofM = 7, widthM = 16;
            float w = (float)widthM * s;
            Caption(g, 16, ground - 18, "Elevation");
            Ground(g, x - 50, x + w + 50, ground);
            Shape(g, new[]
            {
                new PointF(x, ground), new PointF(x, ground - (float)wallM * s), new PointF(x + w / 2, ground - (float)(wallM + roofM) * s),
                new PointF(x + w, ground - (float)wallM * s), new PointF(x + w, ground),
            });

            // Plan: the wall is the ground line seen from above; the area lies in front of it.
            float wallY = ground + 18;
            Caption(g, 16, wallY + 8, "Plan");
            double edge = Rules.Depth(wallM, 1), middle = Rules.Depth(wallM + roofM, 1);
            var area = new[]
            {
                new PointF(x, wallY), new PointF(x + w, wallY), new PointF(x + w, wallY + (float)edge * s),
                new PointF(x + w / 2, wallY + (float)middle * s), new PointF(x, wallY + (float)edge * s),
            };
            Area(g, area);
            g.DrawLine(new Pen(Kit.Colours.Text, 2), x, wallY, x + w, wallY);
            DashV(g, x, ground, wallY);
            DashV(g, x + w, ground, wallY);
            DashV(g, x + w / 2, ground - (float)(wallM + roofM) * s, wallY);
            DimV(g, x + w + 10, wallY, wallY + (float)edge * s, M(edge));
            // The ridge's depth: label under the tip of the area, clear of its outline.
            float tip = wallY + (float)middle * s;
            DimV(g, x + w / 2, wallY, tip, string.Empty);
            SizeF t = KitPaint.Measure(KitFonts.Caption, M(middle));
            g.DrawText(KitFonts.Caption, Kit.Colours.Text2, x + w / 2 - t.Width / 2, tip + 4, M(middle));
            Note(g, x + w + 34, wallY + 16, W - (x + w + 34) - 8,
                $"{F(Rules.GeneralFactor)} × {M(wallM, 0)} at the corners, {F(Rules.GeneralFactor)} × {M(wallM + roofM, 0)} at the ridge");
        }

        /// <summary>Stepped walls: each wall gets its own depth, then one outline per building face.</summary>
        private static void Steps(Graphics g)
        {
            const float s = 4.5f, ground = 112, x = 40;
            const double lowW = 20, lowH = 4, upW = 17, upH = 21;
            double lowD = Rules.Depth(lowH, 1), upD = Rules.Depth(upH, 1);
            float lowFace = x + (float)lowW * s, upFace = x + (float)upW * s;
            float lowEnd = lowFace + (float)lowD * s, upEnd = upFace + (float)upD * s;
            KitPalette c = Kit.Colours;

            Caption(g, 4, 0, "Section");
            Ground(g, 10, 186, ground);
            Shape(g, new[]
            {
                new PointF(x, ground), new PointF(x, ground - (float)upH * s), new PointF(upFace, ground - (float)upH * s),
                new PointF(upFace, ground - (float)lowH * s), new PointF(lowFace, ground - (float)lowH * s), new PointF(lowFace, ground),
            });
            // The upper wall's area, measured from that wall: dashed where it lies inside the building, filled outside it.
            g.DrawRectangle(new Pen(AreaLine, 1) { DashStyle = DashStyles.Dash }, upFace, ground - 4, lowFace - upFace, 8);
            AreaBand(g, lowFace, ground, Math.Max(upEnd, lowEnd) - lowFace);
            DimH(g, upFace, upEnd, ground + 14, $"upper wall {M(upD)}", below: true);
            DimH(g, lowFace, lowEnd, ground + 36, $"lower wall {M(lowD)}", below: true);
            Note(g, 196, 14, W - 202,
                $"The upper storey (H {M(upH, 0)}) is set back {M(lowW - upW, 0)}. Its area is measured from its own wall, so it reaches further than the lower wall's ({M(lowH, 0)}). The part over the lower storey is inside the building and is left out.");

            // Plan: one outline for the face, stepped where the upper wall's area reaches further.
            float py = 186, lowDepth = 64, upDepth = 36;
            Caption(g, 4, py - 16, "Plan");
            Box(g, x, py, (float)lowW * s, lowDepth);
            float uy = py + (lowDepth - upDepth) / 2;
            g.DrawRectangle(new Pen(c.Text, 1) { DashStyle = DashStyles.Dash }, x, uy, (float)upW * s, upDepth);
            Area(g, new[]
            {
                new PointF(lowFace, py), new PointF(lowEnd, py), new PointF(lowEnd, uy), new PointF(upEnd, uy),
                new PointF(upEnd, uy + upDepth), new PointF(lowEnd, uy + upDepth), new PointF(lowEnd, py + lowDepth),
                new PointF(lowFace, py + lowDepth),
            });
            Note(g, 196, py + 4, W - 202, "One outline for the whole face, stepped where the upper storey's area reaches further. Dashed: the upper storey.");
        }

        /// <summary>Where an area may lie: not over another, not on another plot, up to a road's middle.</summary>
        private static void Where(Graphics g)
        {
            KitPalette c = Kit.Colours;
            const float roadTop = 150, roadMid = 170, roadBottom = 190, plotLine = 200;

            // Road with its centre line, and the line between two plots.
            g.FillRectangle(Kit.Mix(c.Surface, c.Text, 0.07f), 0, roadTop, W, roadBottom - roadTop);
            var centre = new Pen(Kit.ToEto(AfColours.Elements[(int)Element.Roads].fallback), 1.5f) { DashStyle = new DashStyle(0, new[] { 6f, 2f, 1f, 2f }) };
            g.DrawLine(centre, 0, roadMid, W, roadMid);
            var plot = new Pen(Kit.ToEto(AfColours.Elements[(int)Element.Plots].fallback), 1.5f) { DashStyle = DashStyles.Dash };
            g.DrawLine(plot, plotLine, 0, plotLine, roadTop);
            Caption(g, 6, roadBottom + 4, "Road");
            Caption(g, 6, roadMid + 3, "Road centre line");
            KitPaint.TextRight(g, KitFonts.Caption, c.Text2, plotLine - 4, 2, "Plot line");

            // Three buildings; each wall's area is a strip in front of it, as deep as that wall needs.
            var a = new RectangleF(40, 86, 60, 44);
            var b = new RectangleF(150, 56, 30, 50);
            var d = new RectangleF(276, 96, 40, 26);
            const float dDepth = 64;
            var areas = new List<RectangleF>();
            foreach (var (box, depth) in new[] { (a, 30f), (b, 30f), (d, dDepth) }) areas.AddRange(Strips(box, depth));
            foreach (RectangleF r in areas) Area(g, r);

            // Clashes, as nbAF fills them.
            Color clash = Kit.ToEto(AfColours.Elements[(int)Element.Clashes].fallback);
            RectangleF overlap = RectangleF.Intersect(Strips(a, 30)[3], Strips(b, 30)[2]);
            RectangleF neighbour = Strips(b, 30)[3];
            neighbour = new RectangleF(plotLine, neighbour.Y, neighbour.Right - plotLine, neighbour.Height);
            RectangleF dBottom = Strips(d, dDepth)[1];
            RectangleF past = new RectangleF(dBottom.X, roadMid, dBottom.Width, dBottom.Bottom - roadMid);
            foreach (RectangleF r in new[] { overlap, neighbour, past }) Clash(g, r, clash);

            foreach (RectangleF box in new[] { a, b, d }) Box(g, box.X, box.Y, box.Width, box.Height);
            g.DrawLine(centre, 0, roadMid, W, roadMid);

            Marker(g, new PointF(overlap.X + overlap.Width / 2, overlap.Y - 8), "1", clash);
            Marker(g, new PointF(neighbour.Right + 9, neighbour.Y + neighbour.Height / 2), "2", clash);
            RectangleF aBottom = Strips(a, 30)[1];
            Marker(g, new PointF(aBottom.X - 9, aBottom.Bottom - 6), "3", Kit.ToEto(AfColours.AllowedColour));
            Marker(g, new PointF(past.X - 9, past.Y + past.Height / 2), "4", clash);
        }

        /// <summary>The four strips in front of a box's walls: top, bottom, left, right.</summary>
        private static RectangleF[] Strips(RectangleF box, float depth) => new[]
        {
            new RectangleF(box.X, box.Y - depth, box.Width, depth),
            new RectangleF(box.X, box.Bottom, box.Width, depth),
            new RectangleF(box.X - depth, box.Y, depth, box.Height),
            new RectangleF(box.Right, box.Y, depth, box.Height),
        };

        // ---- drawing helpers ---------------------------------------------------------------------

        private static Color AreaLine => Kit.ToEto(AfColours.Elements[(int)Element.Areas].fallback);

        private static Color AreaFill => Kit.Mix(Kit.Colours.Surface, AreaLine, Kit.Colours.IsDark ? 0.4f : 0.3f);

        /// <summary>The ground, as a line.</summary>
        private static void Ground(Graphics g, float x1, float x2, float y)
        {
            KitPalette c = Kit.Colours;
            g.DrawLine(new Pen(c.Text2, 1.5f), x1, y, x2, y);
        }

        /// <summary>A building in section or plan: light fill, dark outline.</summary>
        private static void Box(Graphics g, float x, float y, float w, float h) =>
            Shape(g, new[] { new PointF(x, y + h), new PointF(x, y), new PointF(x + w, y), new PointF(x + w, y + h) });

        private static void Shape(Graphics g, PointF[] points)
        {
            KitPalette c = Kit.Colours;
            g.FillPolygon(Kit.Mix(c.Surface, c.Text, 0.12f), points);
            g.DrawPolygon(new Pen(c.Text, 1.5f) { LineJoin = PenLineJoin.Miter }, points);
        }

        /// <summary>An Abstandsfläche in section: a band on the ground in front of a wall.</summary>
        private static void AreaBand(Graphics g, float x, float ground, float length)
        {
            var r = new RectangleF(x, ground - 4, length, 8);
            g.FillRectangle(AreaFill, r);
            g.DrawRectangle(new Pen(AreaLine, 1), r);
        }

        private static void Area(Graphics g, RectangleF r)
        {
            g.FillRectangle(AreaFill, r);
            g.DrawRectangle(new Pen(AreaLine, 1), r);
        }

        private static void Area(Graphics g, PointF[] points)
        {
            g.FillPolygon(AreaFill, points);
            g.DrawPolygon(new Pen(AreaLine, 1.25f), points);
        }

        private static void Clash(Graphics g, RectangleF r, Color clash)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            g.FillRectangle(Kit.Mix(Kit.Colours.Surface, clash, Kit.Colours.IsDark ? 0.55f : 0.45f), r);
            g.DrawRectangle(new Pen(clash, 1.25f), r);
        }

        /// <summary>A numbered marker, like nbAF's clash markers.</summary>
        private static void Marker(Graphics g, PointF centre, string text, Color fill)
        {
            g.FillEllipse(fill, centre.X - 8, centre.Y - 8, 16, 16);
            Font f = KitFonts.Get(10, Weight.Semibold);
            SizeF t = KitPaint.Measure(f, text);
            g.DrawText(f, Colors.White, centre.X - t.Width / 2, centre.Y - t.Height / 2, text);
        }

        /// <summary>A vertical dimension with end ticks and its label beside it.</summary>
        private static void DimV(Graphics g, float x, float y1, float y2, string label, bool strong = false, bool labelLeft = false)
        {
            KitPalette c = Kit.Colours;
            var pen = new Pen(c.Text2, 1);
            g.DrawLine(pen, x, y1, x, y2);
            g.DrawLine(pen, x - 4, y1, x + 4, y1);
            g.DrawLine(pen, x - 4, y2, x + 4, y2);
            Font f = strong ? KitFonts.Get(13, Weight.Semibold) : KitFonts.Caption;
            SizeF t = KitPaint.Measure(f, label);
            float tx = labelLeft ? x - 6 - t.Width : x + 6;
            g.DrawText(f, strong ? c.Text : c.Text2, tx, (y1 + y2) / 2 - t.Height / 2, label);
        }

        /// <summary>A horizontal dimension with end ticks and its label above it.</summary>
        private static void DimH(Graphics g, float x1, float x2, float y, string label, bool below = false)
        {
            KitPalette c = Kit.Colours;
            var pen = new Pen(c.Text2, 1);
            g.DrawLine(pen, x1, y, x2, y);
            g.DrawLine(pen, x1, y - 4, x1, y + 4);
            g.DrawLine(pen, x2, y - 4, x2, y + 4);
            SizeF t = KitPaint.Measure(KitFonts.Caption, label);
            float tx = Math.Max(2, Math.Min(W - 2 - t.Width, (x1 + x2) / 2 - t.Width / 2));
            g.DrawText(KitFonts.Caption, c.Text2, tx, below ? y + 4 : y - 4 - t.Height, label);
        }

        private static void DashH(Graphics g, float x1, float x2, float y) =>
            g.DrawLine(new Pen(Kit.Colours.Text3, 1) { DashStyle = DashStyles.Dash }, x1, y, x2, y);

        private static void DashV(Graphics g, float x, float y1, float y2) =>
            g.DrawLine(new Pen(Kit.Colours.Text3, 1) { DashStyle = DashStyles.Dash }, x, y1, x, y2);

        /// <summary>A short label.</summary>
        private static void Caption(Graphics g, float x, float y, string text) =>
            g.DrawText(KitFonts.Caption, Kit.Colours.Text2, x, y, text);

        /// <summary>Wrapped explanatory text.</summary>
        private static void Note(Graphics g, float x, float y, float width, string text)
        {
            float lh = KitPaint.LineHeight(KitFonts.Caption);
            foreach (string line in KitPaint.Wrap(KitFonts.Caption, text, width))
            {
                g.DrawText(KitFonts.Caption, Kit.Colours.Text, x, y, line);
                y += lh;
            }
        }

        private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        private static string M(double metres, int decimals = 2) =>
            metres.ToString(decimals == 0 ? "0" : decimals == 1 ? "0.0" : "0.00", CultureInfo.InvariantCulture) + " m";

        private static string Share(double share) => Math.Abs(share - 1.0 / 3.0) < 1e-6 ? "1/3" : F(share);
    }
}
