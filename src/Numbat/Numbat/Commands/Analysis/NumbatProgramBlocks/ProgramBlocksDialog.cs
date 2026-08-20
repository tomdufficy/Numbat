using System;
using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;

namespace Numbat.Commands.Analysis.NumbatProgramBlocks
{
    internal sealed class ProgramBlocksDialog : Dialog<bool>
    {
        private readonly DropDown _textModeDropDown;
        private readonly CheckBox _volumeCheckBox;
        private readonly TextArea _editor;
        private readonly Label _statusLabel;

        public IReadOnlyList<ProgramBlockRow> Rows { get; private set; }
        public ProgramBlocksOptions Options { get; private set; }

        public ProgramBlocksDialog()
        {
            Title = "Program Blocks - Paste excel data";
            Resizable = true;
            Padding = 12;
            ClientSize = new Size(780, 700);
            MinimumSize = new Size(720, 650);

            var intro = new Label
            {
                Wrap = WrapMode.Word,
                Text =
                    "Copy rows directly from Excel and paste them below.\n\n" +
                    "The first 3 columns are used, in this order:\n" +
                    "Program Name    |    Area (m2)    |    Category\n" +
                    "Any columns after Category are ignored.\n\n" +
                    "Do not include a header row or category title rows. Area values are always interpreted as square metres, regardless of the Rhino file units.\n\n" +
                    "Example:\n" +
                    "Kitchen    25    Residential\n" +
                    "Bedroom 01    16    Residential\n" +
                    "Meeting Room    40    Office"
            };

            _textModeDropDown = new DropDown
            {
                DataStore = new[] { "Text object", "Text dot" },
                SelectedIndex = 0,
                Width = 150
            };

            _volumeCheckBox = new CheckBox
            {
                Text = "Include 3.5 m-high volume inside each block",
                Checked = false
            };

            _editor = new TextArea
            {
                Wrap = false,
                Font = new Font("Consolas", 10),
                Height = 320
            };

            _statusLabel = new Label
            {
                TextColor = Colors.Red,
                Wrap = WrapMode.Word
            };

            var cancelButton = new Button { Text = "Cancel", Width = 100 };
            var createButton = new Button { Text = "Create", Width = 100 };

            cancelButton.Click += delegate
            {
                Result = false;
                Close();
            };

            createButton.Click += delegate { Accept(); };
            DefaultButton = createButton;
            AbortButton = cancelButton;

            var textModeRow = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 0,
                Items =
                {
                    new StackLayoutItem(_textModeDropDown),
                    new StackLayoutItem(null, true)
                }
            };

            var buttons = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                Items =
                {
                    new StackLayoutItem(null, true),
                    new StackLayoutItem(cancelButton),
                    new StackLayoutItem(createButton)
                }
            };

            var layout = new DynamicLayout
            {
                Spacing = new Size(6, 8)
            };

            layout.AddRow(intro);
            layout.AddRow(new Label { Text = "Text style:" });
            layout.AddRow(textModeRow);
            layout.AddRow(_volumeCheckBox);
            layout.AddRow(_editor);
            layout.AddRow(_statusLabel);
            layout.AddRow(buttons);

            Content = layout;
        }

        private void Accept()
        {
            if (!ProgramBlocksData.TryParseRows(_editor.Text, out var rows, out var errors))
            {
                if (errors.Count == 0)
                {
                    _statusLabel.Text = "Paste at least one valid row.";
                    return;
                }

                int count = Math.Min(6, errors.Count);
                var visible = new List<string>();
                for (int i = 0; i < count; i++)
                    visible.Add(errors[i]);

                if (errors.Count > count)
                    visible.Add($"...and {errors.Count - count} more error(s).");

                _statusLabel.Text = string.Join(Environment.NewLine, visible);
                return;
            }

            Rows = rows;
            Options = new ProgramBlocksOptions
            {
                TextMode = _textModeDropDown.SelectedIndex == 1
                    ? ProgramTextMode.TextDot
                    : ProgramTextMode.TextObject,
                IncludeVolume = _volumeCheckBox.Checked == true
            };

            Result = true;
            Close();
        }
    }
}
