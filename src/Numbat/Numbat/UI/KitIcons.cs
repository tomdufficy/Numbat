using System;
using System.Collections.Generic;
using Eto.Drawing;

namespace Numbat.UI
{
    internal enum KitIcon
    {
        None, Pick, Eye, EyeClosed, Layer, Reset, Folder, Help, Warning, Info, More, Plus, Minus, Close,
        ChevronDown, ChevronRight, Check, Pie, Bars,
    }

    /// <summary>
    /// The kit's icon set: drawn on a 16 px grid with a 1.5 stroke and round caps, so they need no image files,
    /// stay sharp at any interface size and take the colour they are given (currentColor in the design).
    /// </summary>
    internal static class KitIcons
    {
        /// <summary>Draws an icon centred in a box of any size (16 is the design size).</summary>
        public static void Draw(Graphics g, KitIcon icon, RectangleF box, Color color)
        {
            if (icon == KitIcon.None) return;
            float size = Math.Min(box.Width, box.Height);
            float s = size / 16f;
            float ox = box.X + (box.Width - size) / 2;
            float oy = box.Y + (box.Height - size) / 2;
            PointF P(float x, float y) => new PointF(ox + x * s, oy + y * s);
            RectangleF Circle(float cx, float cy, float r) => new RectangleF(ox + (cx - r) * s, oy + (cy - r) * s, 2 * r * s, 2 * r * s);
            void Dot(float cx, float cy) => g.FillEllipse(color, Circle(cx, cy, 0.95f));

            var pen = new Pen(color, 1.5f * s) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            void Line(float x1, float y1, float x2, float y2) => g.DrawLine(pen, P(x1, y1), P(x2, y2));
            void Poly(bool close, params float[] xy)
            {
                using (var path = new GraphicsPath())
                {
                    path.MoveTo(P(xy[0], xy[1]));
                    for (int i = 2; i < xy.Length; i += 2) path.LineTo(P(xy[i], xy[i + 1]));
                    if (close) path.CloseFigure();
                    g.DrawPath(pen, path);
                }
            }
            // A smooth curve: quadratic from (x1, y1) to (x2, y2), pulled towards (cx, cy).
            void Curve(float x1, float y1, float cx, float cy, float x2, float y2)
            {
                var pts = new List<float>();
                for (int i = 0; i <= 12; i++)
                {
                    float t = i / 12f, u = 1 - t;
                    pts.Add(u * u * x1 + 2 * u * t * cx + t * t * x2);
                    pts.Add(u * u * y1 + 2 * u * t * cy + t * t * y2);
                }
                Poly(false, pts.ToArray());
            }

            switch (icon)
            {
                case KitIcon.Pick:
                    g.DrawEllipse(pen, Circle(8, 8, 4.5f));
                    Line(8, 1.5f, 8, 4.5f); Line(8, 11.5f, 8, 14.5f); Line(1.5f, 8, 4.5f, 8); Line(11.5f, 8, 14.5f, 8);
                    break;
                case KitIcon.Eye:
                    // Open eye: both lids, the pupil, and three lashes on the upper lid.
                    Curve(1.5f, 8.5f, 8, 1.5f, 14.5f, 8.5f);
                    Curve(1.5f, 8.5f, 8, 15.5f, 14.5f, 8.5f);
                    g.DrawEllipse(pen, Circle(8, 8.5f, 2.2f));
                    Line(4.75f, 5.9f, 3.6f, 4.0f); Line(8, 5, 8, 2.8f); Line(11.25f, 5.9f, 12.4f, 4.0f);
                    break;
                case KitIcon.EyeClosed:
                    // Closed eye: the lid as one curve, lashes pointing down.
                    Curve(1.5f, 6.5f, 8, 13.5f, 14.5f, 6.5f);
                    Line(4.75f, 9.1f, 3.6f, 11.0f); Line(8, 10, 8, 12.3f); Line(11.25f, 9.1f, 12.4f, 11.0f);
                    break;
                case KitIcon.Layer:
                    Poly(true, 8, 2, 1.5f, 5.5f, 8, 9, 14.5f, 5.5f);
                    Poly(false, 1.5f, 10.5f, 8, 14, 14.5f, 10.5f);
                    break;
                case KitIcon.Reset:
                    g.DrawArc(pen, Circle(8, 8, 5), 225, 315); // open at top-left, where the arrow sits
                    Poly(false, 3, 2.5f, 3, 5.5f, 6, 5.5f);
                    break;
                case KitIcon.Folder:
                    Poly(true, 1.5f, 4, 1.5f, 12.5f, 14.5f, 12.5f, 14.5f, 5.5f, 7.5f, 5.5f, 6, 4);
                    break;
                case KitIcon.Help:
                    g.DrawEllipse(pen, Circle(8, 8, 6.25f));
                    g.DrawArc(pen, Circle(8, 6.6f, 1.9f), 180, 225);
                    Line(9.3f, 7.95f, 8.3f, 8.6f);
                    Line(8.1f, 8.8f, 8, 9.5f);
                    Dot(8, 11.6f);
                    break;
                case KitIcon.Warning:
                    Poly(true, 8, 2, 14.5f, 13.5f, 1.5f, 13.5f);
                    Line(8, 6.5f, 8, 9.5f);
                    Dot(8, 11.6f);
                    break;
                case KitIcon.Info:
                    g.DrawEllipse(pen, Circle(8, 8, 6.25f));
                    Line(8, 7.5f, 8, 11);
                    Dot(8, 5.1f);
                    break;
                case KitIcon.More:
                    g.FillEllipse(color, Circle(3.5f, 8, 1.1f));
                    g.FillEllipse(color, Circle(8, 8, 1.1f));
                    g.FillEllipse(color, Circle(12.5f, 8, 1.1f));
                    break;
                case KitIcon.Plus:
                    Line(8, 4, 8, 12); Line(4, 8, 12, 8);
                    break;
                case KitIcon.Minus:
                    Line(4, 8, 12, 8);
                    break;
                case KitIcon.Close:
                    Line(4.5f, 4.5f, 11.5f, 11.5f); Line(11.5f, 4.5f, 4.5f, 11.5f);
                    break;
                case KitIcon.ChevronDown:
                    Poly(false, 4.5f, 6.5f, 8, 10, 11.5f, 6.5f);
                    break;
                case KitIcon.ChevronRight:
                    Poly(false, 6.5f, 4.5f, 10, 8, 6.5f, 11.5f);
                    break;
                case KitIcon.Check:
                    Poly(false, 3.5f, 8.5f, 6.5f, 11.5f, 12.5f, 4.5f);
                    break;
                case KitIcon.Pie:
                    g.DrawEllipse(pen, Circle(8, 8, 6));
                    using (var wedge = new GraphicsPath())
                    {
                        wedge.MoveTo(P(8, 8));
                        wedge.AddArc(Circle(8, 8, 6), -90, 110);
                        wedge.CloseFigure();
                        g.FillPath(color, wedge);
                    }
                    break;
                case KitIcon.Bars:
                    Line(2.5f, 4, 13.5f, 4); Line(2.5f, 8, 9.5f, 8); Line(2.5f, 12, 11.5f, 12);
                    break;
            }
        }
    }
}
