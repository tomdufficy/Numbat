using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Numbat.Commands.Analysis.NumbatAf
{
    internal enum Element { Areas, Clashes, Plots, Roads, Footprints }

    /// <summary>
    /// Colours of nbAF's preview, baked layers and exports, one per element. Saved per user (Numbat's plugin
    /// settings), so they carry over to every document. These are drawing colours, not window colours: the window
    /// itself takes its colours from the UI kit.
    /// </summary>
    internal static class AfColours
    {
        public static readonly IReadOnlyList<(Element element, string label, string layer, Color fallback)> Elements = new[]
        {
            (Element.Areas, "Abstandsflächen", "Areas", Color.FromArgb(110, 160, 215)),
            (Element.Clashes, "Clashes", "Clashes", Color.FromArgb(225, 35, 35)),
            (Element.Plots, "Plot lines", "Plots", Color.FromArgb(60, 170, 90)),
            (Element.Roads, "Road centre lines", "Roads", Color.FromArgb(120, 95, 170)),
            (Element.Footprints, "Footprints", "Footprints", Color.FromArgb(70, 70, 75)),
        };

        public static string Label(Element e) => Elements.First(x => x.element == e).label;

        public static string Layer(Element e) => Elements.First(x => x.element == e).layer;

        private static string Key(Element e) => AfInfo.SettingsPrefix + "colour." + e;

        public static Color Get(Element e)
        {
            Color fallback = Elements.First(x => x.element == e).fallback;
            int argb = NumbatPlugin.Instance?.Settings.GetInteger(Key(e), fallback.ToArgb()) ?? fallback.ToArgb();
            return Color.FromArgb(255, Color.FromArgb(argb));
        }

        public static void Set(Element e, Color c) => NumbatPlugin.Instance?.Settings.SetInteger(Key(e), c.ToArgb());

        public static void Reset()
        {
            foreach (var x in Elements) Set(x.element, x.fallback);
        }

        private const string KeyLabels = AfInfo.SettingsPrefix + "labels";
        private const string KeyBuildings = AfInfo.SettingsPrefix + "colourByBuilding";

        /// <summary>Tint each building's volumes in its own colour in the preview, to show what nbAF counts as one building.</summary>
        public static bool ColourByBuilding
        {
            get => NumbatPlugin.Instance?.Settings.GetBool(KeyBuildings, true) ?? true;
            set => NumbatPlugin.Instance?.Settings.SetBool(KeyBuildings, value);
        }

        /// <summary>Markers and outlines of things to check (not clashes), such as an area close to a road's centre.</summary>
        public static readonly Color CheckColour = Color.FromArgb(214, 140, 0);

        /// <summary>A building's colour by its colour index (see AfModel: neighbours never share one).</summary>
        public static Color BuildingColour(int index) => BuildingPalette[((index % BuildingPalette.Length) + BuildingPalette.Length) % BuildingPalette.Length];

        /// <summary>Distinct colours for telling buildings apart (no red: that is for clashes).</summary>
        public static readonly Color[] BuildingPalette =
        {
            Color.FromArgb(78, 121, 167), Color.FromArgb(242, 142, 43), Color.FromArgb(89, 161, 79), Color.FromArgb(176, 122, 161),
            Color.FromArgb(237, 201, 72), Color.FromArgb(118, 183, 178), Color.FromArgb(156, 117, 95), Color.FromArgb(255, 157, 167),
            Color.FromArgb(186, 176, 172), Color.FromArgb(107, 110, 207),
        };

        /// <summary>Show a depth label on every wall's area (preview and bake). Off by default: clashes are always marked.</summary>
        public static bool ShowLabels
        {
            get => NumbatPlugin.Instance?.Settings.GetBool(KeyLabels, false) ?? false;
            set => NumbatPlugin.Instance?.Settings.SetBool(KeyLabels, value);
        }
    }
}
