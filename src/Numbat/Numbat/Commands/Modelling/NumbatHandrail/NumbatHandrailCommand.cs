using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Numbat.Commands.Modelling.NumbatHandrail
{
    public class NumbatHandrailCommand : Command
    {
        public static NumbatHandrailCommand Instance { get; private set; }

        public NumbatHandrailCommand()
        {
            Instance = this;
        }

        public override string EnglishName => "nbHandrail";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var gc = new GetObject();
            gc.SetCommandPrompt("Select open flat curve representing handrail path on ground");
            gc.GeometryFilter = ObjectType.Curve;
            gc.EnablePreSelect(true, true);
            gc.Get();

            if (gc.CommandResult() != Result.Success)
                return gc.CommandResult();

            var originalCurve = gc.Object(0).Curve()?.DuplicateCurve();

            if (originalCurve == null)
                return Result.Failure;

            if (originalCurve.IsClosed)
            {
                RhinoApp.WriteLine("nbHandrail currently only supports open curves.");
                return Result.Failure;
            }

            if (!HandrailGenerator.IsCurveFlatInZ(originalCurve, doc.ModelAbsoluteTolerance, out var groundZ))
            {
                RhinoApp.WriteLine("Input curve must be flat/planar in Z.");
                return Result.Failure;
            }

            var height = new OptionDouble(1100.0, true, 100.0);

            var topRailStyleIndex = 1;
            string[] topRailStyleOptions = { "None", "Rectangular", "Round" };
            var boxRailDepth = new OptionDouble(40.0, true, 1.0);
            var boxRailHeight = new OptionDouble(20.0, true, 1.0);
            var topRailDiameter = new OptionDouble(50.0, true, 1.0);

            var bottomRailModeIndex = 1;
            string[] bottomRailOptions = { "None", "Ground", "Raised" };
            var bottomRailHeight = new OptionDouble(100.0, true, 0.0);
            var supportFeet = new OptionToggle(true, "No", "Yes");

            var bayLayoutIndex = 1;
            string[] bayLayoutOptions = { "None", "Automatic" };
            var maxBayLength = new OptionDouble(1200.0, true, 100.0);

            var tabs = new OptionToggle(false, "No", "Yes");
            var tabLength = new OptionDouble(75.0, true, 1.0);

            var infillStyleIndex = 0;
            string[] infillStyleOptions = { "Vertical", "ZigZag", "Panel" };
            var infillWidth = new OptionDouble(10.0, true, 1.0);
            var infillDepth = new OptionDouble(20.0, true, 1.0);
            var maxInfillSpacing = new OptionDouble(100.0, true, 10.0);

            var zigZagDiameter = new OptionDouble(10.0, true, 1.0);
            var zigZagBayLength = new OptionDouble(100.0, true, 10.0);

            var panelGap = new OptionDouble(50.0, true, 0.0);
            var panelFrameSize = new OptionDouble(25.0, true, 1.0);
            var panelSheetThickness = new OptionDouble(5.0, true, 1.0);
            var panelTopGap = new OptionDouble(50.0, true, 0.0);
            var panelBottomGap = new OptionDouble(100.0, true, 0.0);
            var panelFrameConstructionIndex = 0;
            string[] panelFrameConstructionOptions = { "Solid", "Mitred" };

            var previewDims = new OptionToggle(true, "No", "Yes");

            var settings = new HandrailSettings();
            var conduit = new HandrailPreviewConduit();
            conduit.Enabled = true;

            try
            {
                while (true)
                {
                    ApplyOptionValuesToSettings(
                        settings,
                        groundZ,
                        height,
                        topRailStyleIndex,
                        boxRailDepth,
                        boxRailHeight,
                        topRailDiameter,
                        bottomRailModeIndex,
                        bottomRailHeight,
                        supportFeet,
                        bayLayoutIndex,
                        maxBayLength,
                        tabs,
                        tabLength,
                        infillStyleIndex,
                        infillWidth,
                        infillDepth,
                        maxInfillSpacing,
                        zigZagDiameter,
                        zigZagBayLength,
                        panelGap,
                        panelFrameSize,
                        panelSheetThickness,
                        panelTopGap,
                        panelBottomGap,
                        panelFrameConstructionIndex,
                        previewDims
                    );

                    var previewGeometry = HandrailGenerator.CreateHandrailGeometry(originalCurve, settings, doc.ModelAbsoluteTolerance);
                    conduit.PreviewBreps = previewGeometry.AllBreps();
                    conduit.PreviewLabels = previewGeometry.PreviewLabels;
                    conduit.PreviewLines = previewGeometry.PreviewLines;
                    doc.Views.Redraw();

                    var getOptions = new GetOption();
                    getOptions.SetCommandPrompt("Handrail options. Press Enter to create handrail");
                    getOptions.AcceptNothing(true);

                    getOptions.AddOptionDouble("Height", ref height);
                    getOptions.AddOptionList("TopRailStyle", topRailStyleOptions, topRailStyleIndex);

                    if (topRailStyleIndex == 1)
                    {
                        getOptions.AddOptionDouble("BoxRailDepth", ref boxRailDepth);
                        getOptions.AddOptionDouble("BoxRailHeight", ref boxRailHeight);
                    }
                    else if (topRailStyleIndex == 2)
                    {
                        getOptions.AddOptionDouble("TopRailDiameter", ref topRailDiameter);
                        getOptions.AddOptionDouble("BoxRailDepth", ref boxRailDepth);
                        getOptions.AddOptionDouble("BoxRailHeight", ref boxRailHeight);
                    }

                    getOptions.AddOptionList("BottomRail", bottomRailOptions, bottomRailModeIndex);

                    if (bottomRailModeIndex == 2)
                    {
                        getOptions.AddOptionDouble("BottomRailHeight", ref bottomRailHeight);

                        if (bottomRailHeight.CurrentValue > RhinoMath.ZeroTolerance)
                            getOptions.AddOptionToggle("SupportFeet", ref supportFeet);
                    }

                    getOptions.AddOptionList("BayLayout", bayLayoutOptions, bayLayoutIndex);

                    if (bayLayoutIndex == 1)
                        getOptions.AddOptionDouble("MaxBayLength", ref maxBayLength);

                    getOptions.AddOptionToggle("Tabs", ref tabs);

                    if (tabs.CurrentValue)
                        getOptions.AddOptionDouble("TabLength", ref tabLength);

                    getOptions.AddOptionList("InfillStyle", infillStyleOptions, infillStyleIndex);

                    if (infillStyleIndex == 0)
                    {
                        getOptions.AddOptionDouble("InfillWidth", ref infillWidth);
                        getOptions.AddOptionDouble("InfillDepth", ref infillDepth);
                        getOptions.AddOptionDouble("MaxInfillSpacing", ref maxInfillSpacing);
                    }
                    else if (infillStyleIndex == 1)
                    {
                        getOptions.AddOptionDouble("ZigZagDiameter", ref zigZagDiameter);
                        getOptions.AddOptionDouble("ZigZagBayLength", ref zigZagBayLength);
                    }
                    else if (infillStyleIndex == 2)
                    {
                        getOptions.AddOptionDouble("PanelGap", ref panelGap);
                        getOptions.AddOptionDouble("PanelFrameSize", ref panelFrameSize);
                        getOptions.AddOptionDouble("PanelSheetThickness", ref panelSheetThickness);
                        getOptions.AddOptionDouble("PanelTopGap", ref panelTopGap);
                        getOptions.AddOptionDouble("PanelBottomGap", ref panelBottomGap);
                        getOptions.AddOptionList("PanelFrameConstruction", panelFrameConstructionOptions, panelFrameConstructionIndex);
                    }

                    getOptions.AddOptionToggle("PreviewDims", ref previewDims);

                    var result = getOptions.Get();

                    if (result == GetResult.Nothing)
                        break;

                    if (result == GetResult.Cancel)
                        return Result.Cancel;

                    if (result == GetResult.Option)
                    {
                        var option = getOptions.Option();

                        if (option != null)
                        {
                            if (option.EnglishName == "TopRailStyle")
                                topRailStyleIndex = option.CurrentListOptionIndex;

                            if (option.EnglishName == "BottomRail")
                                bottomRailModeIndex = option.CurrentListOptionIndex;

                            if (option.EnglishName == "BayLayout")
                                bayLayoutIndex = option.CurrentListOptionIndex;

                            if (option.EnglishName == "InfillStyle")
                                infillStyleIndex = option.CurrentListOptionIndex;

                            if (option.EnglishName == "PanelFrameConstruction")
                                panelFrameConstructionIndex = option.CurrentListOptionIndex;
                        }
                    }
                }
            }
            finally
            {
                conduit.Enabled = false;
                doc.Views.Redraw();
            }

            ApplyOptionValuesToSettings(
                settings,
                groundZ,
                height,
                topRailStyleIndex,
                boxRailDepth,
                boxRailHeight,
                topRailDiameter,
                bottomRailModeIndex,
                bottomRailHeight,
                supportFeet,
                bayLayoutIndex,
                maxBayLength,
                tabs,
                tabLength,
                infillStyleIndex,
                infillWidth,
                infillDepth,
                maxInfillSpacing,
                zigZagDiameter,
                zigZagBayLength,
                panelGap,
                panelFrameSize,
                panelSheetThickness,
                panelTopGap,
                panelBottomGap,
                panelFrameConstructionIndex,
                previewDims
            );

            var finalGeometry = HandrailGenerator.CreateHandrailGeometry(originalCurve, settings, doc.ModelAbsoluteTolerance);
            HandrailGenerator.AddGeometryToDocument(doc, finalGeometry);

            doc.Views.Redraw();

            RhinoApp.WriteLine("nbHandrail created.");
            RhinoApp.WriteLine($"Height: {settings.Height}");
            RhinoApp.WriteLine($"Top rail style: {topRailStyleOptions[settings.TopRailStyleIndex]}");
            RhinoApp.WriteLine($"Bottom rail: {bottomRailOptions[settings.BottomRailModeIndex]}");
            RhinoApp.WriteLine($"Bay layout: {bayLayoutOptions[settings.BayLayoutIndex]}");

            if (settings.BayLayoutIndex == 1)
                RhinoApp.WriteLine($"Maximum bay length: {settings.MaxBayLength}");
            RhinoApp.WriteLine($"Infill style: {infillStyleOptions[settings.InfillStyleIndex]}");
            RhinoApp.WriteLine($"Tabs: {(settings.Tabs ? "Yes" : "No")}");

            if (finalGeometry.PanelBaysReduced > 0)
                RhinoApp.WriteLine($"Warning: {finalGeometry.PanelBaysReduced} panel bay(s) were reduced because there was insufficient space to maintain the requested panel gap.");

            if (finalGeometry.PanelBaysOmitted > 0)
                RhinoApp.WriteLine($"Warning: {finalGeometry.PanelBaysOmitted} panel bay(s) were omitted because there was insufficient space.");

            return Result.Success;
        }

        private static void ApplyOptionValuesToSettings(
            HandrailSettings settings,
            double groundZ,
            OptionDouble height,
            int topRailStyleIndex,
            OptionDouble boxRailDepth,
            OptionDouble boxRailHeight,
            OptionDouble topRailDiameter,
            int bottomRailModeIndex,
            OptionDouble bottomRailHeight,
            OptionToggle supportFeet,
            int bayLayoutIndex,
            OptionDouble maxBayLength,
            OptionToggle tabs,
            OptionDouble tabLength,
            int infillStyleIndex,
            OptionDouble infillWidth,
            OptionDouble infillDepth,
            OptionDouble maxInfillSpacing,
            OptionDouble zigZagDiameter,
            OptionDouble zigZagBayLength,
            OptionDouble panelGap,
            OptionDouble panelFrameSize,
            OptionDouble panelSheetThickness,
            OptionDouble panelTopGap,
            OptionDouble panelBottomGap,
            int panelFrameConstructionIndex,
            OptionToggle previewDims
        )
        {
            settings.Height = height.CurrentValue;
            settings.TopRailStyleIndex = topRailStyleIndex;
            settings.BoxRailDepth = boxRailDepth.CurrentValue;
            settings.BoxRailHeight = boxRailHeight.CurrentValue;
            settings.TopRailDiameter = topRailDiameter.CurrentValue;

            settings.BottomRailModeIndex = bottomRailModeIndex;
            settings.BottomRailHeight = bottomRailHeight.CurrentValue;
            settings.SupportFeet = bottomRailModeIndex == 2 && bottomRailHeight.CurrentValue > RhinoMath.ZeroTolerance && supportFeet.CurrentValue;

            settings.BayLayoutIndex = bayLayoutIndex;
            settings.MaxBayLength = maxBayLength.CurrentValue;

            settings.Tabs = tabs.CurrentValue;
            settings.TabLength = tabLength.CurrentValue;

            settings.InfillStyleIndex = infillStyleIndex;
            settings.InfillWidth = infillWidth.CurrentValue;
            settings.InfillDepth = infillDepth.CurrentValue;
            settings.MaxInfillSpacing = maxInfillSpacing.CurrentValue;

            settings.ZigZagDiameter = zigZagDiameter.CurrentValue;
            settings.ZigZagBayLength = zigZagBayLength.CurrentValue;

            settings.PanelGap = panelGap.CurrentValue;
            settings.PanelFrameWidth = panelFrameSize.CurrentValue;
            settings.PanelFrameDepth = panelFrameSize.CurrentValue;
            settings.PanelSheetThickness = panelSheetThickness.CurrentValue;
            settings.PanelTopGap = panelTopGap.CurrentValue;
            settings.PanelBottomGap = panelBottomGap.CurrentValue;
            settings.PanelFrameConstructionIndex = panelFrameConstructionIndex;
            settings.PreviewDims = previewDims.CurrentValue;

            settings.GroundZ = groundZ;
        }
    }
}
