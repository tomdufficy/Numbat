using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Numbat.Commands.Analysis.NumbatAf
{
    internal enum Element { Target, Context, Clashes, Plots, Footprints }

    /// <summary>
    /// Colours of nbAF's preview, baked layers and exports, one per element. Saved per user (Numbat's plugin
    /// settings), so they carry over to every document. These are drawing colours, not window colours: the window
    /// itself takes its colours from the UI kit.
    /// </summary>
    internal static class AfColours
    {
        public static readonly IReadOnlyList<(Element element, string label, string layer, Color fallback)> Elements = new[]
        {
            (Element.Target, "Target building", "Target", Color.FromArgb(255, 150, 60)),
            (Element.Context, "Context buildings", "Context", Color.FromArgb(110, 160, 215)),
            (Element.Clashes, "Clashes", "Clashes", Color.FromArgb(225, 35, 35)),
            (Element.Plots, "Plot lines", "Plots", Color.FromArgb(60, 170, 90)),
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

        /// <summary>Show a depth label on every wall's area (preview and bake). Off by default: clashes are always marked.</summary>
        public static bool ShowLabels
        {
            get => NumbatPlugin.Instance?.Settings.GetBool(KeyLabels, false) ?? false;
            set => NumbatPlugin.Instance?.Settings.SetBool(KeyLabels, value);
        }
    }
}
