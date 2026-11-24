using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;

namespace Numbat.UI
{
    /// <summary>
    /// Modal dialog in the kit's layout: the brown title bar (as <see cref="KitWindow"/>), surface background, 16 px
    /// body padding, a divider, and a footer with optional ghost buttons on the left and Cancel / OK on the right
    /// (primary rightmost). Enter presses <see cref="AcceptButton"/>; Esc and the title bar's close button press
    /// <see cref="CancelButton"/>.
    /// </summary>
    internal class KitDialog<T> : Dialog<T>
    {
        public KitDialog(string title)
        {
            Title = title;
            WindowStyle = WindowStyle.None;
            Resizable = false;
            Padding = 0;
            _titleBar = new KitTitleBar(this, title)
            {
                CloseAction = () =>
                {
                    if (CancelButton != null) CancelButton.PerformClick();
                    else Close();
                },
            };
            BackgroundColor = Kit.Colours.Surface;
            KeyDown += (s, e) =>
            {
                if (e.Key == Keys.Enter && AcceptButton != null)
                {
                    e.Handled = true;
                    AcceptButton.PerformClick();
                }
                else if (e.Key == Keys.Escape && CancelButton != null)
                {
                    e.Handled = true;
                    CancelButton.PerformClick();
                }
            };
        }

        private readonly KitTitleBar _titleBar;

        public KitButton AcceptButton { get; set; }
        public KitButton CancelButton { get; set; }

        /// <summary>Sets the body and the footer. <paramref name="footerLeft"/> are usually ghost buttons.</summary>
        public void SetContent(Control body, IEnumerable<Control> footerRight, IEnumerable<Control> footerLeft = null)
        {
            KitPalette c = Kit.Colours;
            var footer = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Padding = new Padding(16, 12),
                VerticalContentAlignment = VerticalAlignment.Center,
                BackgroundColor = c.Surface,
            };
            if (footerLeft != null) foreach (Control b in footerLeft) footer.Items.Add(b);
            footer.Items.Add(new StackLayoutItem(null, true)); // spacer
            foreach (Control b in footerRight) footer.Items.Add(b);

            Content = KitWindow.Frame(_titleBar, new StackLayout
            {
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                BackgroundColor = c.Surface,
                Items =
                {
                    new StackLayoutItem(new Panel { Padding = 16, Content = body, BackgroundColor = c.Surface }, true),
                    KitForm.Divider(),
                    footer,
                },
            });
        }

        /// <summary>Standard Cancel + primary OK pair, wired to the dialog's keys. Returns (ok, cancel).</summary>
        public (KitButton ok, KitButton cancel) StandardButtons(string okText = "OK", string cancelText = "Cancel")
        {
            var ok = new KitButton(okText, ButtonKind.Primary);
            var cancel = new KitButton(cancelText);
            AcceptButton = ok;
            CancelButton = cancel;
            return (ok, cancel);
        }
    }

    /// <summary>Builders for native-control layouts in the kit's grid: 96 px label column, 8 px gaps, 28 px rows.</summary>
    internal static class KitForm
    {
        private static KitPalette C => Kit.Colours;

        public static Label Label(string text) => new Label
        {
            Text = text,
            Font = KitFonts.Scaled(12, Weight.Medium),
            TextColor = C.Text,
            VerticalAlignment = VerticalAlignment.Center,
        };

        /// <summary>Wrapping body text. Give it a width: WPF measures unwrapped text on one line otherwise.</summary>
        public static Label Body(string text, int width, bool secondary = false) => new Label
        {
            Text = text,
            Font = KitFonts.Scaled(12, Weight.Regular),
            TextColor = secondary ? C.Text2 : C.Text,
            Wrap = WrapMode.Word,
            Width = width,
        };

        public static Label Caption(string text, int width = -1) => new Label
        {
            Text = text,
            Font = KitFonts.Scaled(11, Weight.Regular),
            TextColor = C.Text3,
            Wrap = width > 0 ? WrapMode.Word : WrapMode.None,
            Width = width,
            VerticalAlignment = VerticalAlignment.Center,
        };

        /// <summary>UPPERCASE section header.</summary>
        public static Label Section(string text) => new Label
        {
            Text = (text ?? string.Empty).ToUpperInvariant(),
            Font = KitFonts.Scaled(11, Weight.Semibold),
            TextColor = C.Text2,
        };

        /// <summary>
        /// Validation message under a field (error colour, 11 px). Starts as a blank line so the dialog already has
        /// room for it: a dialog doesn't grow when the text is set later, so an empty label would stay invisible.
        /// Set it back to " " (not "") to clear it.
        /// </summary>
        public static Label Error() => new Label
        {
            Text = " ",
            Font = KitFonts.Scaled(11, Weight.Regular),
            TextColor = C.Err,
        };

        public static Control Divider() => new Panel { Height = 1, BackgroundColor = C.Border };

        /// <summary>A control followed by its unit, e.g. [ 12.0 ] mm. Units go after values, never in labels.</summary>
        public static Control WithUnit(Control control, string unit) => new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalContentAlignment = VerticalAlignment.Center,
            Items = { new StackLayoutItem(control, true), Caption(unit) },
        };

        /// <summary>One form row: label (or none) and the control.</summary>
        public static TableRow Row(string label, Control control)
        {
            Control left = string.IsNullOrEmpty(label) ? (Control)new Panel() : Label(label);
            left.Width = (int)(Kit.LabelColumn * Kit.Scale);
            return new TableRow(new TableCell(left), new TableCell(control, true));
        }

        /// <summary>Rows in the label + control grid.</summary>
        public static TableLayout Rows(params TableRow[] rows)
        {
            var table = new TableLayout { Spacing = new Size(8, 8) };
            foreach (TableRow r in rows) table.Rows.Add(r);
            return table;
        }

        /// <summary>Horizontal row of controls with the kit's 8 px gap.</summary>
        public static StackLayout HStack(params Control[] items)
        {
            var s = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 8, VerticalContentAlignment = VerticalAlignment.Center };
            foreach (Control c in items) s.Items.Add(c);
            return s;
        }

        /// <summary>Vertical stack, children stretched to the width.</summary>
        public static StackLayout VStack(int spacing, params Control[] items)
        {
            var s = new StackLayout { Spacing = spacing, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            foreach (Control c in items) s.Items.Add(c);
            return s;
        }
    }
}
