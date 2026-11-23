using System;
using Eto.Drawing;
using Eto.Forms;
using Numbat.UI;
using SdColor = System.Drawing.Color;

namespace Numbat.Commands.Analysis.NumbatGfa
{
    /// <summary>Name + colour prompt used to add or edit a use. Returns null if cancelled.</summary>
    internal static class UseDialog
    {
        public static (string name, SdColor color)? Ask(Window owner, string title, string name, SdColor color,
            Func<string, string> validate, string okText = "OK")
        {
            Display.Load();
            var dialog = new KitDialog<bool>(title);
            var nameBox = new TextBox { Text = name ?? string.Empty, Width = 200 };
            var picker = new ColorPicker { Value = Color.FromArgb(color.R, color.G, color.B) };
            Label error = KitForm.Error();
            var (ok, cancel) = dialog.StandardButtons(okText);

            // Say what's wrong as soon as the name is typed, not only when Add is clicked.
            nameBox.TextChanged += (s, e) => error.Text = validate(nameBox.Text) ?? " ";
            ok.Click += (s, e) =>
            {
                string problem = validate(nameBox.Text);
                if (problem != null)
                {
                    error.Text = problem;
                    return;
                }
                dialog.Close(true);
            };
            cancel.Click += (s, e) => dialog.Close(false);

            dialog.SetContent(
                KitForm.Rows(
                    KitForm.Row("Name", nameBox),
                    KitForm.Row("Colour", KitForm.HStack(picker)),
                    KitForm.Row(null, KitForm.Caption("Mid-tone colours read best.")),
                    KitForm.Row(null, error)),
                new Control[] { cancel, ok });

            nameBox.Focus();
            if (!dialog.ShowModal(owner)) return null;
            Color c = picker.Value;
            return (nameBox.Text.Trim(), SdColor.FromArgb(c.Rb, c.Gb, c.Bb));
        }
    }
}
