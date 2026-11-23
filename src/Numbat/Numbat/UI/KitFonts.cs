using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;

namespace Numbat.UI
{
    internal enum Weight { Regular, Medium, Semibold }

    /// <summary>
    /// Inter in three weights, falling back to Segoe UI (Windows) or the system font (Mac).
    /// Sizes are design pixels; the type scale is below. Fonts are cached.
    /// </summary>
    internal static class KitFonts
    {
        // ---- type scale (size px / weight) ----------------------------------------------------
        public static Font Title => Get(15, Weight.Semibold);      // window title, stat values
        public static Font Section => Get(11, Weight.Semibold);    // UPPERCASE section headers
        public static Font Label => Get(12, Weight.Medium);        // field labels, button text
        public static Font Body => Get(12, Weight.Regular);        // body text and values
        public static Font Caption => Get(11, Weight.Regular);     // captions, units, hints
        public static Font Stat => Get(22, Weight.Semibold);       // the one big figure on a tile

        /// <summary>The same scale multiplied by <see cref="Kit.Scale"/>, for native controls (labels in dialogs).</summary>
        public static Font Scaled(float px, Weight weight) => Get(px * Kit.Scale, weight);

        private static readonly Dictionary<(float, Weight), Font> Cache = new Dictionary<(float, Weight), Font>();
        private static FontFamily _family;

        /// <summary>The family in use: "Inter" when installed, else the platform UI font.</summary>
        public static string FamilyName => Family.Name;

        public static Font Get(float px, Weight weight)
        {
            var key = (px, weight);
            if (Cache.TryGetValue(key, out Font font)) return font;
            font = Create(PxToFontSize(px), weight);
            Cache[key] = font;
            return font;
        }

        /// <summary>
        /// Eto font sizes are points. On Windows a point is 4/3 px; on Mac the drawing unit is the point.
        /// </summary>
        private static float PxToFontSize(float px) => Eto.Platform.Instance.IsMac ? px : px * 0.75f;

        private static FontFamily Family
        {
            get
            {
                if (_family != null) return _family;
                try
                {
                    List<FontFamily> all = Fonts.AvailableFontFamilies.ToList();
                    _family = all.FirstOrDefault(f => f.Name == "Inter");
                    if (_family == null) _family = all.FirstOrDefault(f => f.Name == "Segoe UI");
                }
                catch
                {
                    _family = null;
                }
                if (_family == null) _family = SystemFonts.Default().Family;
                return _family;
            }
        }

        private static Font Create(float size, Weight weight)
        {
            FontFamily family = Family;
            if (weight != Weight.Regular)
            {
                // Medium and Semibold are typefaces, not styles: look them up by name.
                string[] names = weight == Weight.Semibold
                    ? new[] { "SemiBold", "Semibold", "Semi Bold", "DemiBold", "Demibold" }
                    : new[] { "Medium" };
                try
                {
                    FontTypeface face = family.Typefaces.FirstOrDefault(t =>
                        names.Any(n => string.Equals(t.Name, n, StringComparison.OrdinalIgnoreCase)));
                    if (face != null) return new Font(face, size);
                }
                catch
                {
                    // fall through to style-based font
                }
                // No Semibold face: use Bold. No Medium face (Segoe UI has none): Regular is the closer match.
                if (weight == Weight.Semibold) return new Font(family, size, FontStyle.Bold);
            }
            return new Font(family, size);
        }
    }
}
