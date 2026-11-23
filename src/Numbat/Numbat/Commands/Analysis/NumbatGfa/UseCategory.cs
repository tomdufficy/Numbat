using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace Numbat.Commands.Analysis.NumbatGfa
{
    /// <summary>A use (Residential, Office, ...) that volumes can be assigned to, with optional share limits.</summary>
    public sealed class UseCategory
    {
        public UseCategory(string name, Color color)
        {
            Name = name;
            Color = color;
        }

        public string Name { get; internal set; }
        public Color Color { get; set; }

        /// <summary>Minimum share of total GFA, 0..1. Null = no limit.</summary>
        public double? MinShare { get; set; }

        /// <summary>Maximum share of total GFA, 0..1. Null = no limit.</summary>
        public double? MaxShare { get; set; }

        /// <summary>Minimum GFA in the display unit (m², or ft² in imperial models). Null = no limit.</summary>
        public double? MinArea { get; set; }

        /// <summary>Maximum GFA in the display unit (m², or ft² in imperial models). Null = no limit.</summary>
        public double? MaxArea { get; set; }

        public bool HasLimits => MinShare.HasValue || MaxShare.HasValue || MinArea.HasValue || MaxArea.HasValue;

        public void ClearLimits()
        {
            MinShare = MaxShare = MinArea = MaxArea = null;
        }

        /// <summary>The use's sublayer under the GFA layer (By layer mode). Empty until one is made.</summary>
        public Guid LayerId { get; set; }

        /// <summary>
        /// Name of the default use for volumes that count toward the total but have no use decided yet.
        /// It is an ordinary use: it can be renamed, recoloured, limited or removed.
        /// </summary>
        public const string UnassignedName = "Unassigned";

        /// <summary>Pastel defaults, dark enough to carry white text. Unassigned is a neutral grey.</summary>
        public static List<UseCategory> Defaults() => new List<UseCategory>
        {
            new UseCategory("Residential", Color.FromArgb(226, 145, 178)),
            new UseCategory("Office",      Color.FromArgb(129, 168, 214)),
            new UseCategory("Retail",      Color.FromArgb(232, 168, 112)),
            new UseCategory("Hotel",       Color.FromArgb(132, 190, 158)),
            new UseCategory("Amenity",     Color.FromArgb(166, 150, 214)),
            new UseCategory("Parking",     Color.FromArgb(140, 160, 180)),
            new UseCategory(UnassignedName, Color.FromArgb(150, 150, 158)),
        };

        /// <summary>A soft colour for uses found in a document but not in its category list.</summary>
        public static Color FallbackColor(string name)
        {
            var palette = Defaults().Select(c => c.Color).ToArray();
            int hash = 0;
            foreach (char ch in name) hash = unchecked(hash * 31 + ch);
            return palette[Math.Abs(hash % palette.Length)];
        }

        // Stored one category per line: name \t r,g,b \t min share \t max share \t layer id \t min area \t max area
        internal string Serialize() => string.Join("\t",
            Clean(Name),
            $"{Color.R},{Color.G},{Color.B}",
            Num(MinShare),
            Num(MaxShare),
            LayerId == Guid.Empty ? string.Empty : LayerId.ToString(),
            Num(MinArea),
            Num(MaxArea));

        internal static UseCategory Deserialize(string line)
        {
            var parts = line.Split('\t');
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0])) return null;

            var rgb = parts[1].Split(',');
            Color color = rgb.Length == 3
                && int.TryParse(rgb[0], out int r)
                && int.TryParse(rgb[1], out int g)
                && int.TryParse(rgb[2], out int b)
                ? Color.FromArgb(Clamp(r), Clamp(g), Clamp(b))
                : FallbackColor(parts[0]);

            return new UseCategory(parts[0], color)
            {
                MinShare = parts.Length > 2 ? Parse(parts[2]) : null,
                MaxShare = parts.Length > 3 ? Parse(parts[3]) : null,
                LayerId = parts.Length > 4 && Guid.TryParse(parts[4], out Guid id) ? id : Guid.Empty,
                MinArea = parts.Length > 5 ? Parse(parts[5]) : null,
                MaxArea = parts.Length > 6 ? Parse(parts[6]) : null,
            };
        }

        internal static string Clean(string name) =>
            (name ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();

        private static int Clamp(int v) => Math.Max(0, Math.Min(255, v));

        private static string Num(double? v) =>
            v.HasValue ? v.Value.ToString("R", CultureInfo.InvariantCulture) : string.Empty;

        private static double? Parse(string s) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : (double?)null;
    }
}
