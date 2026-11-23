using System.Globalization;
using Eto.Forms;
using Numbat.UI;

namespace Numbat.Commands.Analysis.NumbatGfa
{
    /// <summary>
    /// Small modal prompt for one limit (a percentage or an area). Leaving the box empty removes the limit.
    /// </summary>
    internal static class LimitDialog
    {
        /// <summary>What the user entered. <see cref="Value"/> is null when they cleared the limit.</summary>
        public sealed class Answer
        {
            public double? Value { get; set; }
        }

        /// <summary>Returns the answer, or null if cancelled.</summary>
        /// <param name="current">Current value in the units shown, or null.</param>
        /// <param name="maximum">Largest allowed value (100 for percentages), or null for no upper bound.</param>
        public static Answer Ask(Window owner, string title, string label, string unitCaption, double? current, double? maximum)
        {
            Display.Load();
            var dialog = new KitDialog<Answer>(title);
            var box = new TextBox
            {
                Text = current.HasValue ? current.Value.ToString("#,0.#", CultureInfo.CurrentCulture) : string.Empty,
                Width = 100,
                TextAlignment = TextAlignment.Right,
            };
            Label error = KitForm.Error();
            var (ok, cancel) = dialog.StandardButtons();

            ok.Click += (s, e) =>
            {
                string text = box.Text.Replace("%", string.Empty).Replace("m²", string.Empty).Replace("ft²", string.Empty).Trim();
                if (text.Length == 0)
                {
                    dialog.Close(new Answer { Value = null });
                    return;
                }
                if ((double.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out double v) ||
                     double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out v))
                    && v >= 0 && (!maximum.HasValue || v <= maximum.Value))
                {
                    dialog.Close(new Answer { Value = v });
                }
                else
                {
                    error.Text = maximum.HasValue
                        ? $"Enter a number from 0 to {maximum.Value:0}, or leave it empty for no limit."
                        : "Enter a number of 0 or more, or leave it empty for no limit.";
                }
            };
            cancel.Click += (s, e) => dialog.Close(null);

            dialog.SetContent(
                KitForm.Rows(
                    KitForm.Row(label, KitForm.HStack(box, KitForm.Caption(unitCaption))),
                    KitForm.Row(null, KitForm.Caption("Leave empty for no limit.")),
                    KitForm.Row(null, error)),
                new Control[] { cancel, ok });

            box.Focus();
            return dialog.ShowModal(owner);
        }
    }
}
