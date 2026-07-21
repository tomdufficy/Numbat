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
        private bool _suppressEvents;

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
        public event EventHandler ResetDefaultsRequested;

        public HandrailDialog(HandrailSettings settings)
        {
            Title = "nbHandrail";
            Resizable = true;
            Padding = 10;
            ClientSize = new Size(440, 800);

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

            var resetButton = new Button { Text = "Reset Defaults" };
            resetButton.Click += delegate { RaiseEvent(ResetDefaultsRequested); };

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

            var generalLayout = CreateSectionLayout();
            generalLayout.AddRow(new Label { Text = "Height" }, HeightStepper);

            var topRailLayout = CreateSectionLayout();
            topRailLayout.AddRow(new Label { Text = "Style" }, TopRailStyleDropDown);
            topRailLayout.AddRow(_boxRailDepthLabel, BoxRailDepthStepper);
            topRailLayout.AddRow(_boxRailHeightLabel, BoxRailHeightStepper);
            topRailLayout.AddRow(_topRailDiameterLabel, TopRailDiameterStepper);

            var bottomRailLayout = CreateSectionLayout();
            bottomRailLayout.AddRow(new Label { Text = "Style" }, BottomRailModeDropDown);
            bottomRailLayout.AddRow(_bottomRailHeightLabel, BottomRailHeightStepper);
            bottomRailLayout.AddRow(null, SupportFeetCheckBox);

            var railsLayout = new DynamicLayout
            {
                Padding = 0,
                Spacing = new Size(5, 8),
                DefaultSpacing = new Size(5, 5)
            };
            railsLayout.AddRow(CreateGroup("Top Rail", topRailLayout));
            railsLayout.AddRow(CreateGroup("Bottom Rail", bottomRailLayout));

            var layoutSection = CreateSectionLayout();
            layoutSection.AddRow(new Label { Text = "Bay Layout" }, BayLayoutDropDown);
            layoutSection.AddRow(_maxBayLengthLabel, MaxBayLengthStepper);

            var detailsLayout = CreateSectionLayout();
            detailsLayout.AddRow(null, TabsCheckBox);
            detailsLayout.AddRow(_tabLengthLabel, TabLengthStepper);

            var verticalLayout = CreateSectionLayout();
            verticalLayout.AddRow(_infillWidthLabel, InfillWidthStepper);
            verticalLayout.AddRow(_infillDepthLabel, InfillDepthStepper);
            verticalLayout.AddRow(_maxInfillSpacingLabel, MaxInfillSpacingStepper);

            var zigZagLayout = CreateSectionLayout();
            zigZagLayout.AddRow(_zigZagDiameterLabel, ZigZagDiameterStepper);
            zigZagLayout.AddRow(_zigZagBayLengthLabel, ZigZagBayLengthStepper);

            var panelLayout = CreateSectionLayout();
            panelLayout.AddRow(_panelGapLabel, PanelGapStepper);
            panelLayout.AddRow(_panelFrameSizeLabel, PanelFrameSizeStepper);
            panelLayout.AddRow(_panelSheetThicknessLabel, PanelSheetThicknessStepper);
            panelLayout.AddRow(_panelTopGapLabel, PanelTopGapStepper);
            panelLayout.AddRow(_panelBottomGapLabel, PanelBottomGapStepper);
            panelLayout.AddRow(_panelFrameConstructionLabel, PanelFrameConstructionDropDown);

            var infillLayout = new DynamicLayout
            {
                Padding = 0,
                Spacing = new Size(5, 8),
                DefaultSpacing = new Size(5, 5)
            };
            infillLayout.AddRow(new Label { Text = "Style" }, InfillStyleDropDown);
            infillLayout.AddRow(CreateGroup("Vertical", verticalLayout));
            infillLayout.AddRow(CreateGroup("ZigZag", zigZagLayout));
            infillLayout.AddRow(CreateGroup("Panel", panelLayout));

            var previewLayout = CreateSectionLayout();
            previewLayout.AddRow(null, PreviewDimensionsCheckBox);

            var buttonLayout = new DynamicLayout
            {
                Padding = 0,
                Spacing = new Size(5, 5)
            };
            buttonLayout.AddRow(resetButton, null, createButton, cancelButton);

            var mainLayout = new DynamicLayout
            {
                Padding = 0,
                Spacing = new Size(5, 10),
                DefaultSpacing = new Size(5, 5)
            };
            mainLayout.AddRow(CreateGroup("General", generalLayout));
            mainLayout.AddRow(CreateGroup("Rails", railsLayout));
            mainLayout.AddRow(CreateGroup("Layout", layoutSection));
            mainLayout.AddRow(CreateGroup("Details", detailsLayout));
            mainLayout.AddRow(CreateGroup("Infill", infillLayout));
            mainLayout.AddRow(CreateGroup("Preview", previewLayout));
            mainLayout.AddRow(buttonLayout);

            Content = new Scrollable
            {
                Content = mainLayout,
                ExpandContentWidth = true
            };

            UpdateTopRailControlState();
            UpdateBottomRailControlState();
            UpdateBayControlState();
            UpdateTabsControlState();
            UpdateInfillControlState();
        }

        public void ApplySettings(HandrailSettings settings)
        {
            if (settings == null)
                return;

            _suppressEvents = true;

            try
            {
                HeightStepper.Value = settings.Height;
                TopRailStyleDropDown.SelectedIndex = settings.TopRailStyleIndex;
                BoxRailDepthStepper.Value = settings.BoxRailDepth;
                BoxRailHeightStepper.Value = settings.BoxRailHeight;
                TopRailDiameterStepper.Value = settings.TopRailDiameter;
                BottomRailModeDropDown.SelectedIndex = settings.BottomRailModeIndex;
                BottomRailHeightStepper.Value = settings.BottomRailHeight;
                SupportFeetCheckBox.Checked = settings.SupportFeet;
                BayLayoutDropDown.SelectedIndex = settings.BayLayoutIndex;
                MaxBayLengthStepper.Value = settings.MaxBayLength;
                TabsCheckBox.Checked = settings.Tabs;
                TabLengthStepper.Value = settings.TabLength;
                InfillStyleDropDown.SelectedIndex = settings.InfillStyleIndex;
                InfillWidthStepper.Value = settings.InfillWidth;
                InfillDepthStepper.Value = settings.InfillDepth;
                MaxInfillSpacingStepper.Value = settings.MaxInfillSpacing;
                ZigZagDiameterStepper.Value = settings.ZigZagDiameter;
                ZigZagBayLengthStepper.Value = settings.ZigZagBayLength;
                PanelGapStepper.Value = settings.PanelGap;
                PanelFrameSizeStepper.Value = settings.PanelFrameWidth;
                PanelSheetThicknessStepper.Value = settings.PanelSheetThickness;
                PanelTopGapStepper.Value = settings.PanelTopGap;
                PanelBottomGapStepper.Value = settings.PanelBottomGap;
                PanelFrameConstructionDropDown.SelectedIndex = settings.PanelFrameConstructionIndex;
                PreviewDimensionsCheckBox.Checked = settings.PreviewDims;
            }
            finally
            {
                _suppressEvents = false;
            }

            UpdateTopRailControlState();
            UpdateBottomRailControlState();
            UpdateBayControlState();
            UpdateTabsControlState();
            UpdateInfillControlState();
        }

        private static DynamicLayout CreateSectionLayout()
        {
            return new DynamicLayout
            {
                Padding = 5,
                Spacing = new Size(5, 5),
                DefaultSpacing = new Size(5, 5)
            };
        }

        private static GroupBox CreateGroup(string text, Control content)
        {
            return new GroupBox
            {
                Text = text,
                Padding = 5,
                Content = content
            };
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
            if (_suppressEvents)
                return;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}
