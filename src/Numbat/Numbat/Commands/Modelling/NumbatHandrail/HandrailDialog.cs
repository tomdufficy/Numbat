using System;
using Eto.Drawing;
using Eto.Forms;

namespace Numbat.Commands.Modelling.NumbatHandrail
{
    internal class HandrailDialog : Form
    {
        private readonly Label _boxRailDepthLabel;
        private readonly Label _boxRailHeightLabel;
        private readonly Label _topRailDiameterLabel;
        private readonly Label _bottomRailHeightLabel;
        private readonly Label _maxBayLengthLabel;
        private readonly Label _tabLengthLabel;
        private readonly Label _infillWidthLabel;
        private readonly Label _infillDepthLabel;
        private readonly Label _maxInfillSpacingLabel;
        private readonly Label _zigZagDiameterLabel;
        private readonly Label _zigZagBayLengthLabel;
        private readonly Label _panelGapLabel;
        private readonly Label _panelFrameSizeLabel;
        private readonly Label _panelSheetThicknessLabel;
        private readonly Label _panelTopGapLabel;
        private readonly Label _panelBottomGapLabel;
        private readonly Label _panelFrameConstructionLabel;

        public NumericStepper HeightStepper { get; private set; }
        public DropDown TopRailStyleDropDown { get; private set; }
        public NumericStepper BoxRailDepthStepper { get; private set; }
        public NumericStepper BoxRailHeightStepper { get; private set; }
        public NumericStepper TopRailDiameterStepper { get; private set; }
        public DropDown BottomRailModeDropDown { get; private set; }
        public NumericStepper BottomRailHeightStepper { get; private set; }
        public CheckBox SupportFeetCheckBox { get; private set; }
        public DropDown BayLayoutDropDown { get; private set; }
        public NumericStepper MaxBayLengthStepper { get; private set; }
        public CheckBox TabsCheckBox { get; private set; }
        public NumericStepper TabLengthStepper { get; private set; }
        public DropDown InfillStyleDropDown { get; private set; }
        public NumericStepper InfillWidthStepper { get; private set; }
        public NumericStepper InfillDepthStepper { get; private set; }
        public NumericStepper MaxInfillSpacingStepper { get; private set; }
        public NumericStepper ZigZagDiameterStepper { get; private set; }
        public NumericStepper ZigZagBayLengthStepper { get; private set; }
        public NumericStepper PanelGapStepper { get; private set; }
        public NumericStepper PanelFrameSizeStepper { get; private set; }
        public NumericStepper PanelSheetThicknessStepper { get; private set; }
        public NumericStepper PanelTopGapStepper { get; private set; }
        public NumericStepper PanelBottomGapStepper { get; private set; }
        public DropDown PanelFrameConstructionDropDown { get; private set; }
        public CheckBox PreviewDimensionsCheckBox { get; private set; }

        public bool Accepted { get; private set; }

        public event EventHandler HeightChanged;
        public event EventHandler TopRailChanged;
        public event EventHandler BottomRailChanged;
        public event EventHandler BaysChanged;
        public event EventHandler TabsChanged;
        public event EventHandler InfillChanged;
        public event EventHandler PreviewChanged;

        public HandrailDialog(HandrailSettings settings)
        {
            Title = "nbHandrail";
            Resizable = true;
            Padding = 10;
            ClientSize = new Size(380, 800);

            HeightStepper = CreateStepper(100, 3000, 10, settings.Height);

            TopRailStyleDropDown = new DropDown
            {
                DataStore = new[] { "None", "Rectangular", "Round" },
                SelectedIndex = settings.TopRailStyleIndex
            };

            BoxRailDepthStepper = CreateStepper(1, 1000, 1, settings.BoxRailDepth);
            BoxRailHeightStepper = CreateStepper(1, 1000, 1, settings.BoxRailHeight);
            TopRailDiameterStepper = CreateStepper(1, 1000, 1, settings.TopRailDiameter);

            BottomRailModeDropDown = new DropDown
            {
                DataStore = new[] { "None", "Ground", "Raised" },
                SelectedIndex = settings.BottomRailModeIndex
            };

            BottomRailHeightStepper = CreateStepper(0, 3000, 10, settings.BottomRailHeight);
            SupportFeetCheckBox = new CheckBox
            {
                Text = "Support Feet",
                Checked = settings.SupportFeet
            };

            BayLayoutDropDown = new DropDown
            {
                DataStore = new[] { "None", "Automatic" },
                SelectedIndex = settings.BayLayoutIndex
            };

            MaxBayLengthStepper = CreateStepper(100, 10000, 10, settings.MaxBayLength);

            TabsCheckBox = new CheckBox
            {
                Text = "End Tabs",
                Checked = settings.Tabs
            };

            TabLengthStepper = CreateStepper(1, 1000, 1, settings.TabLength);

            InfillStyleDropDown = new DropDown
            {
                DataStore = new[] { "Vertical", "ZigZag", "Panel", "Sheet", "Empty" },
                SelectedIndex = settings.InfillStyleIndex
            };

            InfillWidthStepper = CreateStepper(1, 1000, 1, settings.InfillWidth);
            InfillDepthStepper = CreateStepper(1, 1000, 1, settings.InfillDepth);
            MaxInfillSpacingStepper = CreateStepper(10, 3000, 10, settings.MaxInfillSpacing);

            ZigZagDiameterStepper = CreateStepper(1, 1000, 1, settings.ZigZagDiameter);
            ZigZagBayLengthStepper = CreateStepper(10, 3000, 10, settings.ZigZagBayLength);

            PanelGapStepper = CreateStepper(0, 1000, 1, settings.PanelGap);
            PanelFrameSizeStepper = CreateStepper(1, 1000, 1, settings.PanelFrameWidth);
            PanelSheetThicknessStepper = CreateStepper(1, 1000, 1, settings.PanelSheetThickness);
            PanelTopGapStepper = CreateStepper(0, 1000, 1, settings.PanelTopGap);
            PanelBottomGapStepper = CreateStepper(0, 1000, 1, settings.PanelBottomGap);
            PanelFrameConstructionDropDown = new DropDown
            {
                DataStore = new[] { "Chamfered", "Single Solid" },
                SelectedIndex = settings.PanelFrameConstructionIndex
            };

            PreviewDimensionsCheckBox = new CheckBox
            {
                Text = "Show Preview Dimensions",
                Checked = settings.PreviewDims
            };

            _boxRailDepthLabel = new Label { Text = "Depth" };
            _boxRailHeightLabel = new Label { Text = "Height" };
            _topRailDiameterLabel = new Label { Text = "Diameter" };
            _bottomRailHeightLabel = new Label { Text = "Height" };
            _maxBayLengthLabel = new Label { Text = "Maximum Bay Length" };
            _tabLengthLabel = new Label { Text = "Tab Length" };
            _infillWidthLabel = new Label { Text = "Width" };
            _infillDepthLabel = new Label { Text = "Depth" };
            _maxInfillSpacingLabel = new Label { Text = "Maximum Spacing" };
            _zigZagDiameterLabel = new Label { Text = "Diameter" };
            _zigZagBayLengthLabel = new Label { Text = "Bay Length" };
            _panelGapLabel = new Label { Text = "Post-to-Panel Gap" };
            _panelFrameSizeLabel = new Label { Text = "Frame Size" };
            _panelSheetThicknessLabel = new Label { Text = "Sheet Thickness" };
            _panelTopGapLabel = new Label { Text = "Top Gap" };
            _panelBottomGapLabel = new Label { Text = "Bottom Gap" };
            _panelFrameConstructionLabel = new Label { Text = "Frame Construction" };

            HeightStepper.ValueChanged += delegate { RaiseEvent(HeightChanged); };

            TopRailStyleDropDown.SelectedIndexChanged += delegate
            {
                UpdateTopRailControlState();
                RaiseEvent(TopRailChanged);
            };
            BoxRailDepthStepper.ValueChanged += delegate { RaiseEvent(TopRailChanged); };
            BoxRailHeightStepper.ValueChanged += delegate { RaiseEvent(TopRailChanged); };
            TopRailDiameterStepper.ValueChanged += delegate { RaiseEvent(TopRailChanged); };

            BottomRailModeDropDown.SelectedIndexChanged += delegate
            {
                UpdateBottomRailControlState();
                RaiseEvent(BottomRailChanged);
            };
            BottomRailHeightStepper.ValueChanged += delegate
            {
                UpdateBottomRailControlState();
                RaiseEvent(BottomRailChanged);
            };
            SupportFeetCheckBox.CheckedChanged += delegate { RaiseEvent(BottomRailChanged); };

            BayLayoutDropDown.SelectedIndexChanged += delegate
            {
                UpdateBayControlState();
                RaiseEvent(BaysChanged);
            };
            MaxBayLengthStepper.ValueChanged += delegate { RaiseEvent(BaysChanged); };

            TabsCheckBox.CheckedChanged += delegate
            {
                UpdateTabsControlState();
                RaiseEvent(TabsChanged);
            };
            TabLengthStepper.ValueChanged += delegate { RaiseEvent(TabsChanged); };

            InfillStyleDropDown.SelectedIndexChanged += delegate
            {
                UpdateInfillControlState();
                RaiseEvent(InfillChanged);
            };
            InfillWidthStepper.ValueChanged += delegate { RaiseEvent(InfillChanged); };
            InfillDepthStepper.ValueChanged += delegate { RaiseEvent(InfillChanged); };
            MaxInfillSpacingStepper.ValueChanged += delegate { RaiseEvent(InfillChanged); };
            ZigZagDiameterStepper.ValueChanged += delegate { RaiseEvent(InfillChanged); };
            ZigZagBayLengthStepper.ValueChanged += delegate { RaiseEvent(InfillChanged); };
            PanelGapStepper.ValueChanged += delegate { RaiseEvent(InfillChanged); };
            PanelFrameSizeStepper.ValueChanged += delegate { RaiseEvent(InfillChanged); };
            PanelSheetThicknessStepper.ValueChanged += delegate { RaiseEvent(InfillChanged); };
            PanelTopGapStepper.ValueChanged += delegate { RaiseEvent(InfillChanged); };
            PanelBottomGapStepper.ValueChanged += delegate { RaiseEvent(InfillChanged); };
            PanelFrameConstructionDropDown.SelectedIndexChanged += delegate { RaiseEvent(InfillChanged); };

            PreviewDimensionsCheckBox.CheckedChanged += delegate { RaiseEvent(PreviewChanged); };

            var createButton = new Button { Text = "Create" };
            createButton.Click += delegate
            {
                Accepted = true;
                Close();
            };

            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Click += delegate
            {
                Accepted = false;
                Close();
            };

            var layout = new DynamicLayout
            {
                Padding = 0,
                Spacing = new Size(5, 8),
                DefaultSpacing = new Size(5, 5)
            };

            layout.AddRow(new Label { Text = "Overall" });
            layout.AddRow(new Label { Text = "Height" }, HeightStepper);

            layout.AddSpace();
            layout.AddRow(new Label { Text = "Top Rail" });
            layout.AddRow(new Label { Text = "Style" }, TopRailStyleDropDown);
            layout.AddRow(_boxRailDepthLabel, BoxRailDepthStepper);
            layout.AddRow(_boxRailHeightLabel, BoxRailHeightStepper);
            layout.AddRow(_topRailDiameterLabel, TopRailDiameterStepper);

            layout.AddSpace();
            layout.AddRow(new Label { Text = "Bottom Rail" });
            layout.AddRow(new Label { Text = "Style" }, BottomRailModeDropDown);
            layout.AddRow(_bottomRailHeightLabel, BottomRailHeightStepper);
            layout.AddRow(null, SupportFeetCheckBox);

            layout.AddSpace();
            layout.AddRow(new Label { Text = "Bays" });
            layout.AddRow(new Label { Text = "Layout" }, BayLayoutDropDown);
            layout.AddRow(_maxBayLengthLabel, MaxBayLengthStepper);

            layout.AddSpace();
            layout.AddRow(new Label { Text = "Tabs" });
            layout.AddRow(null, TabsCheckBox);
            layout.AddRow(_tabLengthLabel, TabLengthStepper);

            layout.AddSpace();
            layout.AddRow(new Label { Text = "Infill" });
            layout.AddRow(new Label { Text = "Style" }, InfillStyleDropDown);
            layout.AddRow(_infillWidthLabel, InfillWidthStepper);
            layout.AddRow(_infillDepthLabel, InfillDepthStepper);
            layout.AddRow(_maxInfillSpacingLabel, MaxInfillSpacingStepper);
            layout.AddRow(_zigZagDiameterLabel, ZigZagDiameterStepper);
            layout.AddRow(_zigZagBayLengthLabel, ZigZagBayLengthStepper);
            layout.AddRow(_panelGapLabel, PanelGapStepper);
            layout.AddRow(_panelFrameSizeLabel, PanelFrameSizeStepper);
            layout.AddRow(_panelSheetThicknessLabel, PanelSheetThicknessStepper);
            layout.AddRow(_panelTopGapLabel, PanelTopGapStepper);
            layout.AddRow(_panelBottomGapLabel, PanelBottomGapStepper);
            layout.AddRow(_panelFrameConstructionLabel, PanelFrameConstructionDropDown);

            layout.AddSpace();
            layout.AddRow(new Label { Text = "Preview" });
            layout.AddRow(null, PreviewDimensionsCheckBox);

            layout.AddSpace();
            layout.AddRow(null, createButton, cancelButton);

            Content = new Scrollable
            {
                Content = layout,
                ExpandContentWidth = true
            };

            UpdateTopRailControlState();
            UpdateBottomRailControlState();
            UpdateBayControlState();
            UpdateTabsControlState();
            UpdateInfillControlState();
        }

        private static NumericStepper CreateStepper(double min, double max, double increment, double value)
        {
            return new NumericStepper
            {
                MinValue = min,
                MaxValue = max,
                Increment = increment,
                DecimalPlaces = 0,
                Value = value
            };
        }

        private void UpdateTopRailControlState()
        {
            var rectangular = TopRailStyleDropDown.SelectedIndex == 1;
            var round = TopRailStyleDropDown.SelectedIndex == 2;

            SetEnabled(rectangular, _boxRailDepthLabel, BoxRailDepthStepper, _boxRailHeightLabel, BoxRailHeightStepper);
            SetEnabled(round, _topRailDiameterLabel, TopRailDiameterStepper);
        }

        private void UpdateBottomRailControlState()
        {
            var raised = BottomRailModeDropDown.SelectedIndex == 2;
            var feetAvailable = raised && BottomRailHeightStepper.Value > 0;

            SetEnabled(raised, _bottomRailHeightLabel, BottomRailHeightStepper);
            SupportFeetCheckBox.Enabled = feetAvailable;
        }

        private void UpdateBayControlState()
        {
            SetEnabled(BayLayoutDropDown.SelectedIndex == 1, _maxBayLengthLabel, MaxBayLengthStepper);
        }

        private void UpdateTabsControlState()
        {
            SetEnabled(TabsCheckBox.Checked == true, _tabLengthLabel, TabLengthStepper);
        }

        private void UpdateInfillControlState()
        {
            var style = InfillStyleDropDown.SelectedIndex;
            var vertical = style == 0;
            var zigZag = style == 1;
            var panel = style == 2;

            SetEnabled(vertical,
                _infillWidthLabel, InfillWidthStepper,
                _infillDepthLabel, InfillDepthStepper,
                _maxInfillSpacingLabel, MaxInfillSpacingStepper);

            SetEnabled(zigZag,
                _zigZagDiameterLabel, ZigZagDiameterStepper,
                _zigZagBayLengthLabel, ZigZagBayLengthStepper);

            SetEnabled(panel,
                _panelGapLabel, PanelGapStepper,
                _panelFrameSizeLabel, PanelFrameSizeStepper,
                _panelSheetThicknessLabel, PanelSheetThicknessStepper,
                _panelTopGapLabel, PanelTopGapStepper,
                _panelBottomGapLabel, PanelBottomGapStepper,
                _panelFrameConstructionLabel, PanelFrameConstructionDropDown);
        }

        private static void SetEnabled(bool enabled, params Control[] controls)
        {
            foreach (var control in controls)
                control.Enabled = enabled;
        }

        private void RaiseEvent(EventHandler handler)
        {
            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}
