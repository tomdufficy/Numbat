using System;
using System.Collections.Generic;
using Eto.Drawing;

namespace Numbat.UI
{
    internal enum ButtonKind
    {
        /// <summary>The one action that writes to the document (Bake, Apply, Export). One per window.</summary>
        Primary,
        /// <summary>Reversible actions: Preview, Browse, Auto range.</summary>
        Secondary,
        /// <summary>Utilities that don't change geometry: Reset, Help, Copy values.</summary>
        Ghost,
        /// <summary>Only for deleting baked geometry; always confirm.</summary>
        Danger,
    }

    /// <summary>Interaction state of one custom-drawn part.</summary>
    internal struct PartState
    {
        public bool Hover, Pressed, Focused, Disabled, On;
    }

    internal enum CalloutKind { Info, Warning, Error, Ok }

    /// <summary>
    /// Stateless drawing helpers for every custom-drawn part of the kit. All coordinates are design px;
    /// <see cref="KitCanvas"/> applies the interface-size scale before calling these.
    /// </summary>
    internal static class KitPaint
    {
        private static KitPalette C => Kit.Colours;

        // ---- basics -----------------------------------------------------------------------------

        public static void Fill(Graphics g, RectangleF r, float radius, Color fill)
        {
            if (radius <= 0) { g.FillRectangle(fill, r); return; }
            using (IGraphicsPath p = GraphicsPath.GetRoundRect(r, radius)) g.FillPath(fill, p);
        }

        /// <summary>A 1 px outline drawn inside the rectangle, so it lines up with fills.</summary>
        public static void Stroke(Graphics g, RectangleF r, float radius, Color color, float width = 1)
        {
            RectangleF inner = RectangleF.Inflate(r, -width / 2, -width / 2);
            using (IGraphicsPath p = GraphicsPath.GetRoundRect(inner, Math.Max(0, radius - width / 2)))
                g.DrawPath(new Pen(color, width), p);
        }

        public static void HLine(Graphics g, float x1, float x2, float y, Color color) =>
            g.FillRectangle(color, x1, y, x2 - x1, 1);

        /// <summary>The focus ring: 2 px accent, 2 px outside the part.</summary>
        public static void FocusRing(Graphics g, RectangleF r, float radius) =>
            Stroke(g, RectangleF.Inflate(r, 4, 4), radius + 4, C.Accent, 2);

        public static SizeF Measure(Font font, string text) =>
            string.IsNullOrEmpty(text) ? SizeF.Empty : font.MeasureString(text);

        /// <summary>Text vertically centred in a band.</summary>
        public static void TextMiddle(Graphics g, Font font, Color color, float x, float top, float height, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            SizeF size = font.MeasureString(text);
            g.DrawText(font, color, x, top + (height - size.Height) / 2, text);
        }

        /// <summary>Text right-aligned at x.</summary>
        public static void TextRight(Graphics g, Font font, Color color, float right, float y, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            g.DrawText(font, color, right - font.MeasureString(text).Width, y, text);
        }

        /// <summary>Shortens text with an ellipsis until it fits the width.</summary>
        public static string Fit(Font font, string text, float width)
        {
            // Half a pixel of tolerance: sub-pixel rounding must not turn a fitting label into "Clo…".
            if (string.IsNullOrEmpty(text) || font.MeasureString(text).Width <= width + 0.5f) return text;
            for (int n = text.Length - 1; n > 0; n--)
            {
                string s = text.Substring(0, n).TrimEnd() + "…";
                if (font.MeasureString(s).Width <= width) return s;
            }
            return "…";
        }

        /// <summary>Greedy word wrap. Returns at least one line.</summary>
        public static List<string> Wrap(Font font, string text, float width)
        {
            var lines = new List<string>();
            foreach (string para in (text ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
            {
                string line = string.Empty;
                foreach (string word in para.Split(' '))
                {
                    string next = line.Length == 0 ? word : line + " " + word;
                    if (line.Length > 0 && font.MeasureString(next).Width > width)
                    {
                        lines.Add(line);
                        line = word;
                    }
                    else line = next;
                }
                lines.Add(line);
            }
            return lines;
        }

        public static float LineHeight(Font font) => font.MeasureString("Hg").Height;

        // ---- mark -------------------------------------------------------------------------------

        /// <summary>
        /// The plugin mark: rounded square (radius ≈ 22 % of size), accent fill, two-letter symbol at ≈ 44 % of
        /// size in Semibold, and the family dot top-right from 22 px up. Monochrome for secondary plugins.
        /// </summary>
        public static void LogoTile(Graphics g, RectangleF r, string letters, bool monochrome = false)
        {
            float size = Math.Min(r.Width, r.Height);
            var tile = new RectangleF(r.X, r.Y, size, size);
            Color fill = monochrome ? C.Text : C.Accent;
            Color ink = monochrome ? C.Surface : C.OnAccent;
            Fill(g, tile, size * 0.22f, fill);

            Font font = KitFonts.Get(size * 0.44f, Weight.Semibold);
            SizeF t = font.MeasureString(letters);
            g.DrawText(font, ink, tile.X + (size - t.Width) / 2, tile.Y + (size - t.Height) / 2, letters);

            if (size >= 22 && !monochrome)
            {
                float d = (float)Math.Round(size / 8);
                g.FillEllipse(Color.FromArgb(ink.Rb, ink.Gb, ink.Bb, 191), tile.Right - 2 * d, tile.Y + d, d, d);
            }
        }

        // ---- buttons ----------------------------------------------------------------------------

        /// <summary>Width a button needs for its content (height is always <see cref="Kit.ControlH"/>).</summary>
        public static float ButtonWidth(string text, KitIcon icon = KitIcon.None, string badge = null)
        {
            bool hasText = !string.IsNullOrEmpty(text);
            if (!hasText) return Kit.ControlH; // icon-only: square
            float w = 24 + Measure(KitFonts.Label, text).Width;
            if (icon != KitIcon.None) w += Kit.IconSize + 6;
            if (!string.IsNullOrEmpty(badge)) w += 6 + BadgeWidth(badge);
            return (float)Math.Ceiling(w);
        }

        private static float BadgeWidth(string badge) => Measure(KitFonts.Get(11, Weight.Semibold), badge).Width + 12;

        /// <summary>
        /// Draws a button. Icon-only when text is empty. <paramref name="state"/>.On draws the toggled /
        /// picking look (accent outline on accent-weak).
        /// </summary>
        public static void Button(Graphics g, RectangleF r, string text, ButtonKind kind, PartState state,
            KitIcon icon = KitIcon.None, string badge = null, bool alignLeft = false)
        {
            KitPalette c = C;
            Color fill, border, ink;
            switch (kind)
            {
                case ButtonKind.Primary:
                    fill = c.Accent; border = c.Accent; ink = c.OnAccent;
                    if (state.Pressed) fill = border = Kit.Mix(c.Accent, c.IsDark ? Colors.White : Colors.Black, 0.1f);
                    else if (state.Hover) fill = border = Kit.Mix(c.Accent, c.IsDark ? Colors.Black : Colors.White, 0.1f);
                    break;
                case ButtonKind.Ghost:
                    fill = state.Pressed ? c.Border : state.Hover ? c.Surface2 : Colors.Transparent;
                    border = Colors.Transparent;
                    ink = state.Hover || state.Pressed ? c.Text : c.Text2;
                    break;
                default:
                    fill = state.Pressed ? c.Border : state.Hover ? c.Surface2 : c.Surface;
                    border = c.Outline;
                    ink = kind == ButtonKind.Danger ? c.Err : c.Text;
                    break;
            }
            if (state.On && kind != ButtonKind.Primary)
            {
                fill = c.AccentWeak; border = c.Accent; ink = c.Accent;
            }
            if (state.Disabled)
            {
                // 45 % opacity against the surface.
                Color under = c.Surface;
                fill = fill.A == 0 ? fill : Kit.Mix(under, fill, 0.45f);
                border = border.A == 0 ? border : Kit.Mix(under, border, 0.45f);
                ink = Kit.Mix(under, ink, 0.45f);
            }

            if (fill.A > 0) Fill(g, r, Kit.RadiusControl, fill);
            if (border.A > 0) Stroke(g, r, Kit.RadiusControl, border);

            bool hasText = !string.IsNullOrEmpty(text);
            float contentW = hasText ? ButtonWidth(text, icon, badge) - 24 : Kit.IconSize;
            float x = alignLeft || contentW > r.Width - 24 ? r.X + (hasText ? 12 : (r.Width - Kit.IconSize) / 2)
                                                           : r.X + (r.Width - contentW) / 2;
            float midY = r.Y + r.Height / 2;
            if (icon != KitIcon.None)
            {
                KitIcons.Draw(g, icon, new RectangleF(x, midY - Kit.IconSize / 2, Kit.IconSize, Kit.IconSize), ink);
                x += Kit.IconSize + 6;
            }
            if (hasText)
            {
                float room = r.Right - 12 - x - (string.IsNullOrEmpty(badge) ? 0 : 6 + BadgeWidth(badge));
                string shown = Fit(KitFonts.Label, text, room);
                TextMiddle(g, KitFonts.Label, ink, x, r.Y, r.Height, shown);
                x += Measure(KitFonts.Label, shown).Width + 6;
            }
            if (!string.IsNullOrEmpty(badge))
            {
                if (alignLeft) x = r.Right - 12 - BadgeWidth(badge);
                Badge(g, new PointF(x, midY), badge);
            }
            if (state.Focused) FocusRing(g, r, Kit.RadiusControl);
        }

        /// <summary>Count badge (accent on accent-weak), vertically centred on <paramref name="leftMid"/>.</summary>
        public static void Badge(Graphics g, PointF leftMid, string text)
        {
            Font f = KitFonts.Get(11, Weight.Semibold);
            SizeF t = f.MeasureString(text);
            var r = new RectangleF(leftMid.X, leftMid.Y - 9, t.Width + 12, 18);
            Fill(g, r, 8, C.AccentWeak);
            g.DrawText(f, C.Accent, r.X + 6, r.Y + (r.Height - t.Height) / 2, text);
        }

        // ---- choices ----------------------------------------------------------------------------

        /// <summary>Segmented control. Returns each segment's rectangle for hit testing.</summary>
        public static RectangleF[] Segmented(Graphics g, RectangleF r, IList<string> options, int selected,
            int hover = -1, int focused = -1, bool disabled = false)
        {
            KitPalette c = C;
            Fill(g, r, 5, c.Surface2);
            Stroke(g, r, 5, c.Border);
            var rects = new RectangleF[options.Count];
            if (options.Count == 0) return rects;
            float w = (r.Width - 4 - 2 * (options.Count - 1)) / options.Count;
            for (int i = 0; i < options.Count; i++)
            {
                var s = new RectangleF(r.X + 2 + i * (w + 2), r.Y + 2, w, r.Height - 4);
                rects[i] = s;
                bool on = i == selected;
                if (on)
                {
                    Fill(g, s, 3, c.Surface);
                    Stroke(g, s, 3, c.Outline);
                }
                else if (i == hover && !disabled)
                {
                    Fill(g, s, 3, c.Bg);
                }
                Color ink = on ? c.Text : c.Text2;
                if (disabled) ink = Kit.Mix(c.Surface2, ink, 0.45f);
                string text = Fit(KitFonts.Label, options[i], s.Width - 8);
                SizeF t = Measure(KitFonts.Label, text);
                g.DrawText(KitFonts.Label, ink, s.X + (s.Width - t.Width) / 2, s.Y + (s.Height - t.Height) / 2, text);
                if (i == focused) FocusRing(g, s, 3);
            }
            return rects;
        }

        /// <summary>On/off switch, 28 × 16.</summary>
        public static void Switch(Graphics g, PointF topLeft, bool on, PartState state)
        {
            KitPalette c = C;
            var r = new RectangleF(topLeft.X, topLeft.Y, 28, 16);
            Color track = on ? c.Accent : c.Outline;
            if (state.Disabled) track = Kit.Mix(c.Surface, track, 0.45f);
            Fill(g, r, 8, track);
            Color knob = on && c.IsDark ? Kit.Hex(0x1A1A1A) : Colors.White;
            g.FillEllipse(knob, on ? r.Right - 14 : r.X + 2, r.Y + 2, 12, 12);
            if (state.Focused) FocusRing(g, r, 8);
        }

        // ---- containers -------------------------------------------------------------------------

        /// <summary>
        /// Header band of a panel or window: optional mark, title, version, then room for icon buttons on the right.
        /// Pass null <paramref name="letters"/> for no mark. Numbat windows use no mark until commands get their own icons.
        /// </summary>
        public static void Header(Graphics g, float width, string letters, string title, string version = null)
        {
            KitPalette c = C;
            g.FillRectangle(c.Surface, 0, 0, width, Kit.HeaderH);
            HLine(g, 0, width, Kit.HeaderH - 1, c.Border);
            float x = Kit.Pad;
            if (!string.IsNullOrEmpty(letters))
            {
                LogoTile(g, new RectangleF(Kit.Pad, (Kit.HeaderH - 22) / 2, 22, 22), letters);
                x += 22 + 8;
            }
            TextMiddle(g, KitFonts.Title, c.Text, x, 0, Kit.HeaderH - 1, title);
            if (!string.IsNullOrEmpty(version))
            {
                x += Measure(KitFonts.Title, title).Width + 6;
                TextMiddle(g, KitFonts.Caption, c.Text3, x, 1, Kit.HeaderH - 1, version);
            }
        }

        /// <summary>
        /// Rectangles for header icon buttons, right to left from the window's right edge (6 px inset, 2 px apart).
        /// </summary>
        public static RectangleF HeaderButton(float width, int indexFromRight) =>
            new RectangleF(width - 6 - Kit.ControlH - indexFromRight * (Kit.ControlH + 2),
                (Kit.HeaderH - 1 - Kit.ControlH) / 2, Kit.ControlH, Kit.ControlH);

        /// <summary>UPPERCASE section header. Returns its height (28).</summary>
        public static float Section(Graphics g, float x, float y, string text)
        {
            TextMiddle(g, KitFonts.Section, C.Text2, x, y, Kit.ControlH, (text ?? string.Empty).ToUpperInvariant());
            return Kit.ControlH;
        }

        /// <summary>Summary tile: caption over a value, on surface-2. Optional third line under the value.</summary>
        public static void Tile(Graphics g, RectangleF r, string caption, string value, string sub = null,
            bool big = false, Color? subColor = null)
        {
            KitPalette c = C;
            Fill(g, r, Kit.RadiusControl, c.Surface2);
            float x = r.X + 8, y = r.Y + 8;
            g.DrawText(KitFonts.Caption, c.Text3, x, y, Fit(KitFonts.Caption, caption, r.Width - 16));
            y += LineHeight(KitFonts.Caption) + 2;

            Font vf = big ? KitFonts.Stat : KitFonts.Title;
            // Big figures step down until they fit the tile.
            foreach (float px in big ? new[] { 22f, 18f, 15f } : new[] { 15f, 13f })
            {
                vf = KitFonts.Get(px, Weight.Semibold);
                if (Measure(vf, value).Width <= r.Width - 16) break;
            }
            g.DrawText(vf, c.Text, x, y, Fit(vf, value, r.Width - 16));
            y += LineHeight(vf);
            if (!string.IsNullOrEmpty(sub))
            {
                g.DrawText(KitFonts.Caption, subColor ?? c.Text3, x, y + 1, Fit(KitFonts.Caption, sub, r.Width - 16));
            }
        }

        /// <summary>Height a tile needs for the given content.</summary>
        public static float TileHeight(bool big, bool withSub) =>
            8 + LineHeight(KitFonts.Caption) + 2 + LineHeight(big ? KitFonts.Stat : KitFonts.Title)
              + (withSub ? LineHeight(KitFonts.Caption) + 1 : 0) + 8;

        /// <summary>
        /// Status box: icon + wrapped text on surface-2 with a border. Never colour alone: the icon and words carry
        /// the meaning. If <paramref name="link"/> is set, a ghost link sits under the text; its rectangle is returned
        /// through <paramref name="linkRect"/>.
        /// </summary>
        public static void Callout(Graphics g, RectangleF r, CalloutKind kind, string text, string link,
            PartState linkState, out RectangleF linkRect)
        {
            KitPalette c = C;
            Fill(g, r, Kit.RadiusControl, c.Surface2);
            Stroke(g, r, Kit.RadiusControl, c.Border);
            Color iconColor = kind == CalloutKind.Warning ? c.Warn : kind == CalloutKind.Error ? c.Err
                            : kind == CalloutKind.Ok ? c.Ok : c.Text3;
            KitIcon icon = kind == CalloutKind.Info ? KitIcon.Info : kind == CalloutKind.Ok ? KitIcon.Check : KitIcon.Warning;
            KitIcons.Draw(g, icon, new RectangleF(r.X + 10, r.Y + 9, 16, 16), iconColor);

            float x = r.X + 10 + 16 + 8;
            float y = r.Y + 8;
            float lh = LineHeight(KitFonts.Body);
            foreach (string line in Wrap(KitFonts.Body, text, r.Right - 10 - x))
            {
                g.DrawText(KitFonts.Body, c.Text, x, y, line);
                y += lh;
            }
            linkRect = RectangleF.Empty;
            if (!string.IsNullOrEmpty(link))
            {
                SizeF t = Measure(KitFonts.Label, link);
                linkRect = new RectangleF(x - 6, y + 4, t.Width + 12, 24);
                Color fill = linkState.Pressed ? c.Border : linkState.Hover ? c.Bg : Colors.Transparent;
                if (fill.A > 0) Fill(g, linkRect, Kit.RadiusControl, fill);
                TextMiddle(g, KitFonts.Label, c.Accent, linkRect.X + 6, linkRect.Y, linkRect.Height, link);
                if (linkState.Focused) FocusRing(g, linkRect, Kit.RadiusControl);
            }
        }

        /// <summary>Height <see cref="Callout"/> needs at a width.</summary>
        public static float CalloutHeight(float width, string text, bool withLink)
        {
            int lines = Wrap(KitFonts.Body, text, width - 10 - 16 - 8 - 10).Count;
            return 8 + Math.Max(16, lines * LineHeight(KitFonts.Body)) + (withLink ? 4 + 24 : 0) + 8;
        }

        /// <summary>Small status line in a footer: coloured dot plus words.</summary>
        public static void StatusLine(Graphics g, float x, float y, Color dot, string text, float maxWidth)
        {
            float lh = LineHeight(KitFonts.Caption);
            g.FillEllipse(dot, x, y + (lh - 6) / 2, 6, 6);
            g.DrawText(KitFonts.Caption, C.Text3, x + 12, y, Fit(KitFonts.Caption, text, maxWidth - 12));
        }

        /// <summary>Thin bar on a track (bar lists, shares, progress): 4 px high, rounded.</summary>
        public static void Bar(Graphics g, RectangleF track, double fraction, Color fill)
        {
            Fill(g, track, track.Height / 2, C.Border);
            float w = (float)(track.Width * Math.Max(0, Math.Min(1, fraction)));
            if (w > 0.5f) Fill(g, new RectangleF(track.X, track.Y, Math.Max(w, track.Height), track.Height), track.Height / 2, fill);
        }

        /// <summary>Pill with a colour dot and a name, e.g. a legend entry or a pickable category.</summary>
        public static void Chip(Graphics g, RectangleF r, Color dot, string text, PartState state)
        {
            KitPalette c = C;
            Fill(g, r, r.Height / 2, state.Pressed ? c.Border : state.Hover ? c.Surface2 : c.Surface);
            Stroke(g, r, r.Height / 2, c.Border);
            g.FillEllipse(dot, r.X + 8, r.Y + (r.Height - 8) / 2, 8, 8);
            TextMiddle(g, KitFonts.Caption, c.Text, r.X + 22, r.Y, r.Height, Fit(KitFonts.Caption, text, r.Width - 30));
            if (state.Focused) FocusRing(g, r, r.Height / 2);
        }

        public static float ChipWidth(string text) => (float)Math.Ceiling(Measure(KitFonts.Caption, text).Width) + 30;
    }
}
