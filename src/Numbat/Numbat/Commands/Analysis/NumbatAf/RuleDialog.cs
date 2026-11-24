using System;
using System.Globalization;
using Eto.Forms;
using Numbat.UI;

namespace Numbat.Commands.Analysis.NumbatAf
{
    /// <summary>Small modal prompt for one rule value. Accepts decimals with . or , and fractions like 1/3.</summary>
    internal static class RuleDialog
    {
        /// <summary>Returns the new value, or null if cancelled.</summary>
        public static double? Ask(Window owner, RuleField field, double current)
        {
            Display.Load();
            var dialog = new KitDialog<double?>($"{AfInfo.Name} – {field.Label}");
            var box = new TextBox { Text = Show(field, current), Width = 100, TextAlignment = TextAlignment.Right };
            Label error = KitForm.Error();
            var (ok, cancel) = dialog.StandardButtons();

            ok.Click += (s, e) =>
            {
                double? v = Parse(box.Text);
                if (v.HasValue && v.Value >= field.Min && v.Value <= field.Max) dialog.Close(v.Value);
                else error.Text = $"Enter a number from {field.Min.ToString(CultureInfo.CurrentCulture)} to {field.Max.ToString(CultureInfo.CurrentCulture)}.";
            };
            cancel.Click += (s, e) => dialog.Close(null);

            dialog.SetContent(
                KitForm.Rows(
                    KitForm.Row("Value", KitForm.HStack(box, KitForm.Caption(Unit(field)))),
                    KitForm.Row(null, KitForm.Caption($"HBauO default: {field.Format(field.Default)} ({field.Paragraph})")),
                    KitForm.Row(null, error)),
                new Control[] { cancel, ok });

            box.Focus();
            return dialog.ShowModal(owner);
        }

        private static string Unit(RuleField field)
        {
            string shown = field.Format(field.Default);
            if (shown.EndsWith(" H")) return "× H";
            if (shown.EndsWith(" m")) return "m";
            if (shown.EndsWith("°")) return "°";
            return "of the roof height";
        }

        private static string Show(RuleField field, double v) =>
            field.Key == AfRules.RoofShareKey && Math.Abs(v - 1.0 / 3.0) < 1e-6 ? "1/3" : v.ToString("0.####", CultureInfo.CurrentCulture);

        private static double? Parse(string text)
        {
            text = (text ?? "").Replace("H", "").Replace("m", "").Replace("°", "").Trim();
            int slash = text.IndexOf('/');
            if (slash > 0)
            {
                double? a = Parse(text.Substring(0, slash)), b = Parse(text.Substring(slash + 1));
                return a.HasValue && b.HasValue && b.Value != 0 ? a / b : null;
            }
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double v)) return v;
            if (double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return null;
        }
    }
}
