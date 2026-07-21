using System;
using Eto.Drawing;
using Eto.Forms;

namespace Numbat.Commands.Modelling.NumbatHandrail
{
    internal class HandrailDialog : Form
    {
        public NumericStepper HeightStepper { get; private set; }
        public bool Accepted { get; private set; }

        public event EventHandler HeightChanged;

        public HandrailDialog(HandrailSettings settings)
        {
            Title = "nbHandrail";
            Resizable = true;
            Padding = 10;
            ClientSize = new Size(300, 130);

            HeightStepper = new NumericStepper
            {
                MinValue = 100,
                MaxValue = 3000,
                Increment = 10,
                DecimalPlaces = 0,
                Value = settings.Height
            };

            HeightStepper.ValueChanged += (sender, e) =>
            {
                HeightChanged?.Invoke(this, EventArgs.Empty);
            };

            var createButton = new Button
            {
                Text = "Create"
            };

            createButton.Click += (sender, e) =>
            {
                Accepted = true;
                Close();
            };

            var cancelButton = new Button
            {
                Text = "Cancel"
            };

            cancelButton.Click += (sender, e) =>
            {
                Accepted = false;
                Close();
            };

            var layout = new DynamicLayout
            {
                Spacing = new Size(5, 10),
                DefaultSpacing = new Size(5, 5)
            };

            layout.AddRow(
                new Label { Text = "Height" },
                HeightStepper
            );

            layout.AddSpace();

            layout.AddRow(
                null,
                createButton,
                cancelButton
            );

            Content = layout;
        }
    }
}
