using System;
using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;

namespace Numbat.UI
{
    /// <summary>
    /// Numbat's mark for window title bars: a logo-brown rounded square with a white lower-case "nb". Drawn in code at
    /// every size Windows asks for, so it stays sharp at any display scaling and needs no image file.
    /// </summary>
    internal static class KitMark
    {
        private static Icon _icon;

        /// <summary>The mark as a window icon (16 to 48 px, for 100 % to 300 % display scaling).</summary>
        public static Icon Icon => _icon ?? (_icon = Make());

        private static Icon Make()
        {
            var frames = new List<IconFrame>();
            foreach (var (scale, px) in new[] { (1f, 16), (1.25f, 20), (1.5f, 24), (2f, 32), (3f, 48) })
            {
                frames.Add(IconFrame.FromControlObject(scale, Draw(px)));
            }
            return new Icon(frames.ToArray());
        }

        /// <summary>The mark on a square bitmap of <paramref name="px"/> pixels.</summary>
        public static Bitmap Draw(int px)
        {
            var bmp = new Bitmap(px, px, PixelFormat.Format32bppRgba);
            using (var g = new Graphics(bmp))
            {
                g.AntiAlias = true;
                g.Clear(Colors.Transparent);
                using (IGraphicsPath tile = GraphicsPath.GetRoundRect(new RectangleF(0, 0, px, px), px * 0.24f))
                {
                    g.FillPath(Kit.Hex(0x6C4B33), tile);
                }
                // Big, bold letters: at 16 px anything thinner turns into a smudge.
                Font font = KitFonts.Get(px * 0.62f, Weight.Semibold);
                SizeF size = font.MeasureString("nb");
                g.DrawText(font, Colors.White, (px - size.Width) / 2, (px - size.Height) / 2 - px * 0.03f, "nb");
            }
            return bmp;
        }
    }

    /// <summary>
    /// Floating tool window in Numbat's style: the normal Windows title bar with Numbat's mark and the full name
    /// ("nbAF – Abstandsflächen"), close button only, on top of Rhino. The content draws its own kit header with the
    /// window-level buttons (? and ⋯).
    /// </summary>
    internal class KitWindow : FloatingForm
    {
        public KitWindow(string title)
        {
            Title = title;
            WindowStyle = WindowStyle.Default; // Utility windows have no icon, so the mark wouldn't show
            Icon = KitMark.Icon;
            Minimizable = false;
            Maximizable = false;
            ShowInTaskbar = false;
            Topmost = true;
            BackgroundColor = Kit.Colours.Surface;
        }

        /// <summary>The window's content.</summary>
        public Control Body
        {
            get => Content;
            set => Content = value;
        }
    }
}
