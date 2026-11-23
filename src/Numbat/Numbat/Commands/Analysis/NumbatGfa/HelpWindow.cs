using System.IO;
using System.Linq;
using System.Reflection;
using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;
using Numbat.UI;

namespace Numbat.Commands.Analysis.NumbatGfa
{
    /// <summary>
    /// Shows docs/nbGFA.md (embedded in the plugin) as a styled, scrollable window.
    /// Understands a small Markdown subset: # title, ## heading, "- " bullets and paragraphs.
    /// Styled with the Rhino UI kit: surface background, Inter type scale, follows Rhino's dark mode.
    /// </summary>
    public sealed class HelpWindow : FloatingForm
    {
        private const int TextWidth = 380;

        private static HelpWindow _instance;

        private static KitPalette C => Kit.Colours;

        private HelpWindow()
        {
            Color background = C.Surface;
            Title = $"{GfaInfo.Name} – How to";
            WindowStyle = WindowStyle.Utility;
            Minimizable = false;
            Maximizable = false;
            ShowInTaskbar = false;
            Topmost = true;
            BackgroundColor = background;
            ClientSize = new Size((int)((TextWidth + 48) * Kit.Scale), (int)(560 * Kit.Scale));

            var stack = new StackLayout { Padding = new Padding(20), Spacing = 6, BackgroundColor = background };
            foreach (Control c in Render(LoadText())) stack.Items.Add(c);
            stack.Items.Add(new Panel { Height = 8 });
            stack.Items.Add(KitForm.Caption(GfaInfo.Copyright));

            Content = new Scrollable { Content = stack, Border = BorderType.None, BackgroundColor = background };
            Closed += (s, e) => _instance = null;
        }

        public static void ShowWindow()
        {
            if (_instance == null)
            {
                Display.Load();
                _instance = new HelpWindow { Owner = RhinoEtoApp.MainWindow };
                _instance.Show();
            }
            _instance.BringToFront();
        }

        private static string LoadText()
        {
            Assembly asm = typeof(HelpWindow).Assembly;
            string name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("nbGFA.md"));
            if (name == null) return "# Help not found\n\nThe how-to guide was not included in this build.";
            using (var reader = new StreamReader(asm.GetManifestResourceStream(name)))
            {
                return reader.ReadToEnd();
            }
        }

        private static System.Collections.Generic.IEnumerable<Control> Render(string markdown)
        {
            var paragraph = new System.Text.StringBuilder();

            foreach (string raw in markdown.Replace("\r", string.Empty).Split('\n'))
            {
                string line = raw.Trim();

                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("- "))
                {
                    if (paragraph.Length > 0)
                    {
                        yield return Text(paragraph.ToString(), Body, C.Text2);
                        paragraph.Clear();
                    }
                }

                if (line.StartsWith("## "))
                {
                    yield return new Panel { Height = 10 };
                    yield return Text(line.Substring(3), KitFonts.Scaled(13, Weight.Semibold), C.Text);
                }
                else if (line.StartsWith("# "))
                {
                    yield return Text(line.Substring(2), KitFonts.Scaled(18, Weight.Semibold), C.Text);
                }
                else if (line.StartsWith("- "))
                {
                    yield return Text("•  " + line.Substring(2), Body, C.Text2, indent: 8);
                }
                else if (line.Length > 0)
                {
                    if (paragraph.Length > 0) paragraph.Append(' ');
                    paragraph.Append(line);
                }
            }
            if (paragraph.Length > 0) yield return Text(paragraph.ToString(), Body, C.Text2);
        }

        private static Font Body => KitFonts.Scaled(12, Weight.Regular);

        private static Control Text(string text, Font font, Color color, int indent = 0)
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
