using System;
using Eto.Forms;
using Numbat.UI;

namespace Numbat.UI
{
    /// <summary>
    /// The UI kit's display options (interface size, stronger outlines), saved in Numbat's plugin settings.
    /// Dark mode needs no option: the kit follows Rhino.
    /// </summary>
    internal static class Display
    {
        private const string KeyScale = "ui.scale";
        private const string KeyStrong = "ui.strong";

        /// <summary>Applies the saved options to the kit. Call before showing any Numbat window or dialog.</summary>
        public static void Load()
        {
            var settings = NumbatPlugin.Instance.Settings;
            double scale = settings.GetDouble(KeyScale, 1.0);
            Kit.Scale = 1f;
            foreach (float s in Kit.Scales)
            {
                if (Math.Abs(s - scale) < 0.01) Kit.Scale = s;
            }
            Kit.StrongOutlines = settings.GetBool(KeyStrong, false);
        }

        /// <summary>Adds "Interface size" and "Stronger outlines" to a menu. <paramref name="changed"/> runs after a change.</summary>
        public static void AddMenuItems(ContextMenu menu, Action changed)
        {
            var size = new SubMenuItem { Text = "Interface size" };
            RadioMenuItem group = null;
            foreach (float s in Kit.Scales)
            {
                float scale = s;
                var item = new RadioMenuItem(group) { Text = $"{scale * 100:0} %", Checked = Math.Abs(Kit.Scale - scale) < 0.01 };
                group = group ?? item;
                item.Click += (o, e) =>
                {
                    NumbatPlugin.Instance.Settings.SetDouble(KeyScale, scale);
                    Kit.Scale = scale;
                    changed();
                };
                size.Items.Add(item);
            }
            menu.Items.Add(size);

            var strong = new CheckMenuItem { Text = "Stronger outlines", Checked = Kit.StrongOutlines };
            strong.Click += (o, e) =>
            {
                Kit.StrongOutlines = !Kit.StrongOutlines;
                NumbatPlugin.Instance.Settings.SetBool(KeyStrong, Kit.StrongOutlines);
                changed();
            };
            menu.Items.Add(strong);
        }
    }
}
