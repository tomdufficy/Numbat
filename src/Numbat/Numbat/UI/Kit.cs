using System;
using Eto.Drawing;

namespace Numbat.UI
{
    /// <summary>
    /// Colour tokens for one theme. Names follow the UI kit design:
    /// bg, surface, surface-2, border, outline, text, text-2, text-3, accent, accent-weak, on-accent, ok, warn, err.
    /// </summary>
    internal sealed class KitPalette
    {
        public bool IsDark;
        public Color Bg, Surface, Surface2, Border, Outline;
        public Color Text, Text2, Text3;
        public Color Accent, AccentWeak, OnAccent;
        public Color Ok, Warn, Err;
    }

    /// <summary>
    /// The kit's shared state: theme, display options and the size grid. Every value is in design pixels
    /// (logical px at 100 % interface size); custom-drawn parts multiply by <see cref="Scale"/>.
    /// </summary>
    internal static class Kit
    {
        // ---- grid (4 px base) -------------------------------------------------------------
        public const float Unit = 4;
        public const float ControlH = 28;
        public const float HeaderH = 44;
        public const float LabelColumn = 96;
        public const float Pad = 12;
        public const float Gap = 8;
        public const float RadiusControl = 4;
        public const float RadiusCard = 6;
        public const float IconSize = 16;

        // ---- display options (Settings > Display in the design) -----------------------------

        /// <summary>Interface size: 1, 1.15 or 1.3. Plugins load this from their own settings.</summary>
        public static float Scale { get; set; } = 1f;

        public static readonly float[] Scales = { 1f, 1.15f, 1.3f };

        /// <summary>Darker borders on fields, buttons and dividers.</summary>
        public static bool StrongOutlines { get; set; }

        /// <summary>Diverging ramp: moss / orange (false) or blue / orange (true).</summary>
        public static bool BlueOrange { get; set; }

        /// <summary>Forces a theme (for testing). Null follows Rhino.</summary>
        public static bool? ForceDark { get; set; }

        /// <summary>True when Rhino runs in dark mode.</summary>
        public static bool IsDark
        {
            get
            {
                if (ForceDark.HasValue) return ForceDark.Value;
                try { return Rhino.Runtime.HostUtils.RunningInDarkMode; }
                catch { return false; }
            }
        }

        /// <summary>The token set for the current theme and options. Cheap; read it at paint time.</summary>
        public static KitPalette Colours => IsDark ? (StrongOutlines ? DarkStrong : Dark) : (StrongOutlines ? LightStrong : Light);

        private static readonly KitPalette Light = MakeLight(false);
        private static readonly KitPalette LightStrong = MakeLight(true);
        private static readonly KitPalette Dark = MakeDark(false);
        private static readonly KitPalette DarkStrong = MakeDark(true);

        private static KitPalette MakeLight(bool strong) => new KitPalette
        {
            IsDark = false,
            Bg = Hex(0xEBEBEA), Surface = Hex(0xFAFAFA), Surface2 = Hex(0xF1F1F0),
            Border = strong ? Hex(0x8A8A87) : Hex(0xDADAD8),
            Outline = strong ? Hex(0x5E5E5B) : Hex(0x8A8A87),
            Text = Hex(0x1A1A1A), Text2 = Hex(0x545454), Text3 = Hex(0x6A6A6A),
            // Accent = logo Brown; AccentWeak = a pale tint of logo Tan. Brown on Surface/AccentWeak is 6.6:1 or better.
            Accent = Hex(0x6C4B33), AccentWeak = Hex(0xF3EBDF), OnAccent = Hex(0xFFFFFF),
            Ok = Hex(0x15803D), Warn = Hex(0x8A5A06), Err = Hex(0xC2410C),
        };

        private static KitPalette MakeDark(bool strong) => new KitPalette
        {
            IsDark = true,
            Bg = Hex(0x1C1C1C), Surface = Hex(0x262626), Surface2 = Hex(0x303030),
            Border = strong ? Hex(0x5A5A5A) : Hex(0x3A3A3A),
            Outline = strong ? Hex(0xA0A0A0) : Hex(0x7A7A7A),
            Text = Hex(0xEDEDED), Text2 = Hex(0xB5B5B5), Text3 = Hex(0x9A9A9A),
            // Accent = logo Tan; AccentWeak = a dark brown. Tan on Surface/AccentWeak is 5.2:1 or better.
            Accent = Hex(0xC79C69), AccentWeak = Hex(0x3A2E24), OnAccent = Hex(0x1A1A1A),
            Ok = Hex(0x4ADE80), Warn = Hex(0xFACC15), Err = Hex(0xFB923C),
        };

        // ---- ramps --------------------------------------------------------------------------

        /// <summary>Sequential ramp for magnitude (density, thickness, length), low to high.</summary>
        public static readonly Color[] Sequential =
            { Hex(0xF6EEDF), Hex(0xE2C8A3), Hex(0xC79C69), Hex(0x8F6142), Hex(0x4E3524) }; // cream to brown, from the logo

        /// <summary>Diverging ramp for deviation, negative to positive. Follows <see cref="BlueOrange"/>.</summary>
        public static Color[] Diverging => BlueOrange
            ? new[] { Hex(0x1E4AA8), Hex(0x7E9FE3), Hex(0xB5B5B2), Hex(0xF0A56E), Hex(0xB8480B) }
            : new[] { Hex(0x2C4A3E), Hex(0x8AB0A0), Hex(0xB5B5B2), Hex(0xF2B27A), Hex(0xD17A26) };

        /// <summary>Colour at t (0..1) along a ramp, interpolating between its stops.</summary>
        public static Color Sample(Color[] ramp, double t)
        {
            t = Math.Max(0, Math.Min(1, t)) * (ramp.Length - 1);
            int i = Math.Min((int)t, ramp.Length - 2);
            return Mix(ramp[i], ramp[i + 1], (float)(t - i));
        }

        // ---- colour helpers -----------------------------------------------------------------

        public static Color Hex(int rgb) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

        /// <summary>Linear mix from a (t = 0) to b (t = 1).</summary>
        public static Color Mix(Color a, Color b, float t) => new Color(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t);

        public static Color ToEto(System.Drawing.Color c) => Color.FromArgb(c.R, c.G, c.B);

        /// <summary>Text colour (near-black or white) that reads best on a fill colour.</summary>
        public static Color TextOn(Color fill)
        {
            double l = 0.2126 * fill.R + 0.7152 * fill.G + 0.0722 * fill.B;
            return l > 0.55 ? Hex(0x1A1A1A) : Hex(0xFFFFFF);
        }
    }
}
