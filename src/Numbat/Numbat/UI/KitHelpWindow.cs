using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;

namespace Numbat.UI
{
    /// <summary>
    /// A floating window that shows a Markdown guide embedded in the plugin, styled with the kit (surface background,
    /// type scale, follows Rhino's dark mode). Understands a small subset: # title, ## heading, "- " bullets and
    /// paragraphs. Optional extra controls go after the guide, then the copyright line. One window per guide.
    /// </summary>
    internal sealed class KitHelpWindow : KitWindow
    {
        public const int TextWidth = 380;

        private static readonly Dictionary<string, KitHelpWindow> Open = new Dictionary<string, KitHelpWindow>();

        private static KitPalette C => Kit.Colours;

        private KitHelpWindow(string title, string markdown, Func<IEnumerable<Control>> extra, string copyright) : base(title)
        {
            Color background = C.Surface;
            Resizable = true;
            ClientSize = new Size((int)((TextWidth + 48) * Kit.Scale), (int)(560 * Kit.Scale));

            var stack = new StackLayout { Padding = new Padding(20), Spacing = 6, BackgroundColor = background };
            foreach (Control c in Render(markdown)) stack.Items.Add(c);
            if (extra != null) foreach (Control c in extra()) stack.Items.Add(c);
            stack.Items.Add(new Panel { Height = 8 });
            stack.Items.Add(KitForm.Caption(copyright));

            Body = new Scrollable { Content = stack, Border = BorderType.None, BackgroundColor = background };
        }

        /// <summary>Opens the guide, or brings it to the front if it is already open.</summary>
        /// <param name="resourceSuffix">End of the embedded resource name, e.g. "nbGFA.md".</param>
        /// <param name="extra">Controls added after the guide (for example a list of sources), or null.</param>
        public static void Show(string title, string resourceSuffix, string copyright, Func<IEnumerable<Control>> extra = null)
        {
            if (!Open.TryGetValue(resourceSuffix, out KitHelpWindow window))
            {
                Display.Load();
                window = new KitHelpWindow(title, LoadText(resourceSuffix), extra, copyright) { Owner = RhinoEtoApp.MainWindow };
                window.Closed += (s, e) => Open.Remove(resourceSuffix);
                Open[resourceSuffix] = window;
                window.Show();
            }
            window.BringToFront();
        }

        private static string LoadText(string resourceSuffix)
        {
            Assembly asm = typeof(KitHelpWindow).Assembly;
            string name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(resourceSuffix));
            if (name == null) return "# Help not found\n\nThe guide was not included in this build.";
            using (var reader = new StreamReader(asm.GetManifestResourceStream(name)))
            {
                return reader.ReadToEnd();
            }
        }

        private static IEnumerable<Control> Render(string markdown)
        {
            var paragraph = new StringBuilder();

            foreach (string raw in markdown.Replace("\r", string.Empty).Split('\n'))
            {
                string line = raw.Trim();

                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("- "))
                {
                    if (paragraph.Length > 0)
                    {
                        yield return Text(paragraph.ToString(), BodyFont, C.Text2);
                        paragraph.Clear();
                    }
                }

                if (line.StartsWith("## "))
                {
                    yield return Heading(line.Substring(3));
                }
                else if (line.StartsWith("# "))
                {
                    yield return Text(line.Substring(2), KitFonts.Scaled(18, Weight.Semibold), C.Text);
                }
                else if (line.StartsWith("- "))
                {
                    yield return Text("•  " + line.Substring(2), BodyFont, C.Text2, indent: 8);
                }
                else if (line.Length > 0)
                {
                    if (paragraph.Length > 0) paragraph.Append(' ');
                    paragraph.Append(line);
                }
            }
            if (paragraph.Length > 0) yield return Text(paragraph.ToString(), BodyFont, C.Text2);
        }

        public static Font BodyFont => KitFonts.Scaled(12, Weight.Regular);

        /// <summary>A section heading in the guide's style, with space above it.</summary>
        public static Control Heading(string text) => new StackLayout
        {
            Items = { new Panel { Height = 10 }, Text(text, KitFonts.Scaled(13, Weight.Semibold), C.Text) },
        };

        /// <summary>Wrapping text at the guide's width.</summary>
        public static Control Text(string text, Font font, Color color, int indent = 0)
        {
            var label = new Label
            {
                Text = text.Replace("**", string.Empty).Replace("`", string.Empty),
                Font = font,
                TextColor = color,
                Wrap = WrapMode.Word,
                Width = (int)((TextWidth - indent) * Kit.Scale),
            };
            return indent == 0 ? (Control)label : new Panel { Padding = new Padding(indent, 0, 0, 0), Content = label };
        }
    }
}
