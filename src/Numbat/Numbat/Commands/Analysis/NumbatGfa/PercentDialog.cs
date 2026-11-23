using System.Globalization;
using Eto.Forms;
using Numbat.UI;

namespace Numbat.Commands.Analysis.NumbatGfa
{
    /// <summary>Tiny modal prompt for a percentage. Returns a fraction (0..1), or null if cancelled.</summary>
    internal static class PercentDialog
    {
        public static double? Ask(Window owner, string title, double? current)
        {
            Display.Load();
            var dialog = new KitDialog<double?>(title);
            var box = new TextBox
            {
                Text = current.HasValue ? (current.Value * 100).ToString("0.#", CultureInfo.CurrentCulture) : string.Empty,
                Width = 80,
                TextAlignment = TextAlignment.Right,
            };
            Label error = KitForm.Error();
            var (ok, cancel) = dialog.StandardButtons();

            ok.Click += (s, e) =>
            {
                string text = box.Text.Replace("%", string.Empty).Trim();
                if ((double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double v) ||
                     double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                    && v >= 0 && v <= 100)
                {
                    dialog.Close(v / 100.0);
                }
                else
                {
                    error.Text = "Enter a number from 0 to 100.";
                }
            };
            cancel.Click += (s, e) => dialog.Close(null);

            dialog.SetContent(
                KitForm.Rows(
                    KitForm.Row("Share", KitForm.HStack(box, KitForm.Caption("% of total GFA"))),
                    KitForm.Row(null, error)),
                new Control[] { cancel, ok });

            box.Focus();
            return dialog.ShowModal(owner);
        }
    }
}
