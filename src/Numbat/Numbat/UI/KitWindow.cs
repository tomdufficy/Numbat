using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private static readonly (float scale, int px)[] Sizes = { (1f, 16), (1.25f, 20), (1.5f, 24), (2f, 32), (3f, 48) };

        private static Icon _icon;
        private static byte[] _ico;

        /// <summary>The mark as a window icon (16 to 48 px, for 100 % to 300 % display scaling).</summary>
        public static Icon Icon => _icon ?? (_icon = new Icon(Sizes.Select(s => new IconFrame(s.scale, Draw(s.px))).ToArray()));

        /// <summary>
        /// Puts the mark in a window's title bar. Inside Rhino, Eto doesn't pass a window's icon on to the Windows
        /// window, so on Windows the mark is also set on the native window directly (by reflection, so nothing here
        /// depends on WPF at compile time). Elsewhere the Eto icon is all there is.
        /// </summary>
        public static void ApplyTo(Window window)
        {
            window.Icon = Icon;
            try
            {
                object native = window.ControlObject;
                System.Reflection.PropertyInfo prop = native?.GetType().GetProperty("Icon");
                if (prop == null || prop.PropertyType.FullName != "System.Windows.Media.ImageSource") return;
                Type frame = prop.PropertyType.Assembly.GetType("System.Windows.Media.Imaging.BitmapFrame");
                System.Reflection.MethodInfo create = frame?.GetMethods().FirstOrDefault(m =>
                    m.Name == "Create" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(Stream));
                if (create == null) return;
                prop.SetValue(native, create.Invoke(null, new object[] { new MemoryStream(Ico()) }));
            }
            catch
            {
                // Not WPF, or it changed: the window keeps Rhino's icon. Purely cosmetic.
            }
        }

        /// <summary>The mark as an .ico file in memory, one PNG frame per size.</summary>
        private static byte[] Ico()
        {
            if (_ico != null) return _ico;
            var pngs = new List<byte[]>();
            foreach (var (_, px) in Sizes)
            {
                using (var ms = new MemoryStream())
                {
                    Draw(px).Save(ms, ImageFormat.Png);
                    pngs.Add(ms.ToArray());
                }
            }
            using (var ico = new MemoryStream())
            using (var w = new BinaryWriter(ico))
            {
                w.Write((short)0);
                w.Write((short)1); // icon
                w.Write((short)pngs.Count);
                int offset = 6 + 16 * pngs.Count;
                for (int i = 0; i < pngs.Count; i++)
                {
                    w.Write((byte)Sizes[i].px);
                    w.Write((byte)Sizes[i].px);
                    w.Write((byte)0);
                    w.Write((byte)0);
                    w.Write((short)1);
                    w.Write((short)32);
                    w.Write(pngs[i].Length);
                    w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (byte[] png in pngs) w.Write(png);
                w.Flush();
                return _ico = ico.ToArray();
            }
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
            KitMark.ApplyTo(this);
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
