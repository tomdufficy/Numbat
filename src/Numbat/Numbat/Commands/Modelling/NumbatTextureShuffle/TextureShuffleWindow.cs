using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.UI;
using Numbat.UI;

namespace Numbat.Commands.Modelling.NumbatTextureShuffle
{
    /// <summary>
    /// Small floating window: select objects in Rhino, then click Shuffle as often as you like until the textures
    /// look right. Every click is one undo step. Settings are remembered between sessions.
    /// </summary>
    internal sealed class TextureShuffleWindow : KitWindow
    {
        private const int ContentWidth = 300;
        private const string KeyX = "textureShuffle.window.x";
        private const string KeyY = "textureShuffle.window.y";

        private static TextureShuffleWindow _instance;

        private readonly TextureShuffleSettings _settings = TextureShuffleSettings.Load();
        private readonly Label _status;
        private readonly Label _unitX, _unitY, _unitZ;
        private readonly Label _shiftUnits;
        private readonly NumericStepper _sizeX, _sizeY, _sizeZ, _maxShift;
        private double _mmToModel = 1;
        private bool _loading;

        private TextureShuffleWindow() : base("nbTextureShuffle – Texture Shuffle")
        {
            Resizable = false;
            KitPalette c = Kit.Colours;
            int width = (int)(ContentWidth * Kit.Scale);

            _status = KitForm.Body(string.Empty, width, secondary: true);

            // ---- shuffle buttons: the main reason the window exists, so they come first.
            var both = new KitButton("Shuffle", ButtonKind.Primary, KitIcon.Reset)
            {
                Tip = "Move the texture on the selected objects by a new random amount",
            };
            both.Click += (s, e) => Run(ShuffleAction.Shift);

            // ---- mapping box
            _sizeX = Stepper(_settings.SizeX);
            _sizeY = Stepper(_settings.SizeY);
            _sizeZ = Stepper(_settings.SizeZ);
            _unitX = KitForm.Caption("mm");
            _unitY = KitForm.Caption("mm");
            _unitZ = KitForm.Caption("mm");

            var align = new KitSegmented(new[] { "World", "Object" }, _settings.AlignToObject ? 1 : 0);
            align.SelectedIndexChanged += (s, e) =>
            {
                _settings.AlignToObject = align.SelectedIndex == 1;
                _settings.Save();
            };

            var sameForAll = new KitSwitch("Same for all selected", _settings.SameForAll);
            sameForAll.CheckedChanged += (s, e) => { _settings.SameForAll = sameForAll.Checked; _settings.Save(); };

            var apply = new KitButton("Straighten")
            {
                Tip = "Put the box mapping back at its starting position: no random shift or turn",
            };
            apply.Click += (s, e) => Run(ShuffleAction.Apply);
            var reset = new KitButton("Remove mapping", ButtonKind.Ghost)
            {
                Tip = "Take the box mapping off; Rhino's default surface mapping is used again",
            };
            reset.Click += (s, e) => Run(ShuffleAction.Reset);

            // ---- shift
            _maxShift = Stepper(_settings.MaxOffset, min: 0);
            _shiftUnits = KitForm.Caption("mm");
            KitButton ax = AxisToggle("X", () => _settings.OffsetX, v => _settings.OffsetX = v);
            KitButton ay = AxisToggle("Y", () => _settings.OffsetY, v => _settings.OffsetY = v);
            KitButton az = AxisToggle("Z", () => _settings.OffsetZ, v => _settings.OffsetZ = v);

            Body = new StackLayout
            {
                Padding = new Padding(16, 16, 16, 24),
                Spacing = 12,
                Width = width + 32,
                BackgroundColor = c.Surface,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items =
                {
                    _status,
                    both,
                    KitForm.Caption("Each click picks new random values. Ctrl+Z undoes a click.", width),
                    KitForm.Divider(),
                    KitForm.Section("Mapping box"),
                    KitForm.Rows(
                        KitForm.Row("Repeat X", KitForm.HStack(_sizeX, _unitX)),
                        KitForm.Row("Repeat Y", KitForm.HStack(_sizeY, _unitY)),
                        KitForm.Row("Repeat Z", KitForm.HStack(_sizeZ, _unitZ)),
                        KitForm.Row("Align to", align),
                        KitForm.Row(null, sameForAll)),
                    KitForm.Divider(),
                    KitForm.Section("Shift"),
                    KitForm.Rows(
                        KitForm.Row("Up to", KitForm.HStack(_maxShift, _shiftUnits)),
                        KitForm.Row("Along", KitForm.HStack(ax, ay, az))),
                    KitForm.Divider(),
                    KitForm.HStack(apply, reset),
                },
            };

            RhinoDoc.SelectObjects += OnSelectionChanged;
            RhinoDoc.DeselectObjects += OnSelectionChanged;
            RhinoDoc.DeselectAllObjects += OnSelectionChanged;
            RhinoDoc.ActiveDocumentChanged += OnDocumentChanged;
            // The window sizes itself to its content, which can come out a little short with custom-drawn controls;
            // add breathing room once it is shown so the bottom buttons are never cut off.
            Shown += (s, e) => ClientSize = new Size(ClientSize.Width, ClientSize.Height + (int)(24 * Kit.Scale));
            Closing += (s, e) => SaveLocation();
            Closed += (s, e) =>
            {
                RhinoDoc.SelectObjects -= OnSelectionChanged;
                RhinoDoc.DeselectObjects -= OnSelectionChanged;
                RhinoDoc.DeselectAllObjects -= OnSelectionChanged;
                RhinoDoc.ActiveDocumentChanged -= OnDocumentChanged;
                _instance = null;
            };

            RefreshUnits();
            RefreshStatus();
        }

        /// <summary>Opens the window, or brings it to the front if it is already open.</summary>
        public static void ShowWindow()
        {
            if (_instance != null)
            {
                _instance.BringToFront();
                return;
            }
            Display.Load();
            _instance = new TextureShuffleWindow { Owner = RhinoEtoApp.MainWindow };
            _instance.RestoreLocation();
            _instance.Show();
            _instance.BringToFront();
        }

        // ------------------------------------------------------------------ actions

        private void Run(ShuffleAction action)
        {
            RhinoDoc doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            ReadInputs();
            List<RhinoObject> targets = SelectedTargets(doc);
            if (targets.Count == 0)
            {
                _status.Text = "Select surfaces, polysurfaces, meshes or SubDs in Rhino first.";
                return;
            }
            try
            {
                int n = TextureShuffler.Run(doc, targets, _settings, action);
                RhinoApp.WriteLine($"nbTextureShuffle: {Describe(action)} on {n} object(s).");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"nbTextureShuffle: could not change the mapping: {ex.Message}");
            }
            RefreshStatus();
        }

        private static string Describe(ShuffleAction action)
        {
            switch (action)
            {
                case ShuffleAction.Apply: return "straightened mapping";
                case ShuffleAction.Resize: return "resized mapping";
                case ShuffleAction.Shift: return "shuffled texture";
                case ShuffleAction.Reset: return "removed box mapping";
                default: return "shuffled texture";
            }
        }

        private static List<RhinoObject> SelectedTargets(RhinoDoc doc) =>
            doc.Objects.GetSelectedObjects(false, false).Where(TextureShuffler.IsTarget).ToList();

        // ------------------------------------------------------------------ inputs

        private NumericStepper Stepper(double mm, double min = 1)
        {
            var stepper = new NumericStepper
            {
                MinValue = min,
                DecimalPlaces = 0,
                MaximumDecimalPlaces = 3,
                Width = (int)(64 * Kit.Scale),
                Value = mm,
            };
            stepper.ValueChanged += (s, e) =>
            {
                if (_loading) return;
                ReadInputs();
                if (stepper != _maxShift) ResizeSelection();
            };
            return stepper;
        }

        /// <summary>A new repeat size applies straight away to selected objects that already have a box mapping.</summary>
        private void ResizeSelection()
        {
            RhinoDoc doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            List<RhinoObject> targets = SelectedTargets(doc).Where(o => TextureShuffler.TryGetMappingBox(o, out _)).ToList();
            if (targets.Count == 0) return;
            TextureShuffler.Run(doc, targets, _settings, ShuffleAction.Resize);
        }

        private KitButton AxisToggle(string axis, Func<bool> get, Action<bool> set)
        {
            var button = new KitButton(axis) { On = get(), Tip = $"Shift along the box's {axis} axis" };
            button.Click += (s, e) =>
            {
                set(!get());
                button.On = get();
                _settings.Save();
            };
            return button;
        }

        /// <summary>Steppers show model units; settings are kept in mm.</summary>
        private void ReadInputs()
        {
            double toMm = 1 / _mmToModel;
            _settings.SizeX = _sizeX.Value * toMm;
            _settings.SizeY = _sizeY.Value * toMm;
            _settings.SizeZ = _sizeZ.Value * toMm;
            _settings.MaxOffset = _maxShift.Value * toMm;
            _settings.Save();
        }

        private void RefreshUnits()
        {
            RhinoDoc doc = RhinoDoc.ActiveDoc;
            _mmToModel = doc != null ? TextureShuffleSettings.MmToModel(doc) : 1;
            if (_mmToModel <= 0 || double.IsNaN(_mmToModel)) _mmToModel = 1;
            string unit = doc?.GetUnitSystemName(true, false, true, true) ?? "mm";

            _loading = true;
            int decimals = _mmToModel < 0.01 ? 3 : _mmToModel < 1 ? 1 : 0;
            foreach ((NumericStepper stepper, double mm) in new[]
                     {
                         (_sizeX, _settings.SizeX), (_sizeY, _settings.SizeY), (_sizeZ, _settings.SizeZ),
                         (_maxShift, _settings.MaxOffset),
                     })
            {
                stepper.MinValue = stepper == _maxShift ? 0 : Math.Pow(10, -decimals);
                stepper.DecimalPlaces = decimals;
                stepper.Value = mm * _mmToModel;
            }
            _loading = false;
            _unitX.Text = _unitY.Text = _unitZ.Text = _shiftUnits.Text = unit;
        }

        private void RefreshStatus()
        {
            RhinoDoc doc = RhinoDoc.ActiveDoc;
            int n = doc == null ? 0 : SelectedTargets(doc).Count;
            _status.Text = n == 0
                ? "Select surfaces, polysurfaces, meshes or SubDs in Rhino, then click Shuffle."
                : $"{n} object(s) selected.";
        }

        private void OnSelectionChanged(object sender, EventArgs e)
        {
            RefreshStatus();
        }

        private void OnDocumentChanged(object sender, EventArgs e)
        {
            RefreshUnits();
            RefreshStatus();
        }

        // ------------------------------------------------------------------ position

        private void RestoreLocation()
        {
            var settings = NumbatPlugin.Instance.Settings;
            int x = settings.GetInteger(KeyX, int.MinValue);
            int y = settings.GetInteger(KeyY, int.MinValue);
            if (x == int.MinValue || y == int.MinValue) return;
            var probe = new PointF(x + 40, y + 10);
            if (Screen.Screens.Any(s => s.WorkingArea.Contains(probe))) Location = new Point(x, y);
        }

        private void SaveLocation()
        {
            var settings = NumbatPlugin.Instance.Settings;
            settings.SetInteger(KeyX, Location.X);
            settings.SetInteger(KeyY, Location.Y);
        }
    }
}
