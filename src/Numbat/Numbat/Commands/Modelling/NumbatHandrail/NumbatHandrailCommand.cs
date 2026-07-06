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

            var topRailStyleIndex = 0;
            string[] topRailStyleOptions = { "Rectangular", "Round" };
            var boxRailDepth = new OptionDouble(40.0, true, 1.0);
            var boxRailHeight = new OptionDouble(20.0, true, 1.0);
            var topRailDiameter = new OptionDouble(50.0, true, 1.0);

            var bottomRailModeIndex = 1;
            string[] bottomRailOptions = { "None", "Ground", "Raised" };
            var bottomRailHeight = new OptionDouble(100.0, true, 0.0);
            var supportFeet = new OptionToggle(true, "No", "Yes");

            var postPlacementIndex = 0;
            string[] postPlacementOptions = { "Auto", "FixedSpacing" };
            var intermediatePosts = new OptionToggle(false, "No", "Yes");
            var intermediatePostSpacing = new OptionDouble(1000.0, true, 100.0);
            var postSpacing = new OptionDouble(1200.0, true, 100.0);
            var postDistributionIndex = 0;
            string[] postDistributionOptions = { "Equalize", "Exact" };

            var wallTabs = new OptionToggle(false, "No", "Yes");
            var tabLength = new OptionDouble(100.0, true, 1.0);

            var infillStyleIndex = 0;
            string[] infillStyleOptions = { "Vertical", "ZigZag" };
            var infillWidth = new OptionDouble(10.0, true, 1.0);
            var infillDepth = new OptionDouble(20.0, true, 1.0);
            var maxInfillSpacing = new OptionDouble(100.0, true, 10.0);

            var zigZagDiameter = new OptionDouble(10.0, true, 1.0);
            var zigZagBayLength = new OptionDouble(100.0, true, 10.0);

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
                        postPlacementIndex,
                        intermediatePosts,
                        intermediatePostSpacing,
                        postSpacing,
                        postDistributionIndex,
                        wallTabs,
                        tabLength,
                        infillStyleIndex,
                        infillWidth,
                        infillDepth,
                        maxInfillSpacing,
                        zigZagDiameter,
                        zigZagBayLength
                    );

                    var previewGeometry = HandrailGenerator.CreateHandrailGeometry(originalCurve, settings, doc.ModelAbsoluteTolerance);
                    conduit.PreviewBreps = previewGeometry.AllBreps();
                    doc.Views.Redraw();

                    var getOptions = new GetOption();
                    getOptions.SetCommandPrompt("Handrail options. Press Enter to create handrail");
                    getOptions.AcceptNothing(true);

                    getOptions.AddOptionDouble("Height", ref height);
                    getOptions.AddOptionList("TopRailStyle", topRailStyleOptions, topRailStyleIndex);

                    if (topRailStyleIndex == 0)
                    {
                        getOptions.AddOptionDouble("BoxRailDepth", ref boxRailDepth);
                        getOptions.AddOptionDouble("BoxRailHeight", ref boxRailHeight);
                    }
                    else
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

                    getOptions.AddOptionList("PostPlacement", postPlacementOptions, postPlacementIndex);

                    if (postPlacementIndex == 0)
                    {
                        getOptions.AddOptionToggle("IntermediatePosts", ref intermediatePosts);

                        if (intermediatePosts.CurrentValue)
                            getOptions.AddOptionDouble("IntermediatePostSpacing", ref intermediatePostSpacing);
                    }
                    else
                    {
                        getOptions.AddOptionDouble("PostSpacing", ref postSpacing);
                        getOptions.AddOptionList("PostDistribution", postDistributionOptions, postDistributionIndex);
                    }

                    getOptions.AddOptionToggle("WallTabs", ref wallTabs);

                    if (wallTabs.CurrentValue)
                        getOptions.AddOptionDouble("TabLength", ref tabLength);

                    getOptions.AddOptionList("InfillStyle", infillStyleOptions, infillStyleIndex);

                    if (infillStyleIndex == 0)
                    {
                        getOptions.AddOptionDouble("InfillWidth", ref infillWidth);
                        getOptions.AddOptionDouble("InfillDepth", ref infillDepth);
                        getOptions.AddOptionDouble("MaxInfillSpacing", ref maxInfillSpacing);
                    }
                    else
                    {
                        getOptions.AddOptionDouble("ZigZagDiameter", ref zigZagDiameter);
                        getOptions.AddOptionDouble("ZigZagBayLength", ref zigZagBayLength);
                    }

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

                            if (option.EnglishName == "PostPlacement")
                                postPlacementIndex = option.CurrentListOptionIndex;

                            if (option.EnglishName == "PostDistribution")
                                postDistributionIndex = option.CurrentListOptionIndex;

                            if (option.EnglishName == "InfillStyle")
                                infillStyleIndex = option.CurrentListOptionIndex;
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
                postPlacementIndex,
                intermediatePosts,
                intermediatePostSpacing,
                postSpacing,
                postDistributionIndex,
                wallTabs,
                tabLength,
                infillStyleIndex,
                infillWidth,
                infillDepth,
                maxInfillSpacing,
                zigZagDiameter,
                zigZagBayLength
            );

            var finalGeometry = HandrailGenerator.CreateHandrailGeometry(originalCurve, settings, doc.ModelAbsoluteTolerance);
            HandrailGenerator.AddGeometryToDocument(doc, finalGeometry);

            doc.Views.Redraw();

            RhinoApp.WriteLine("nbHandrail created.");
            RhinoApp.WriteLine($"Height: {settings.Height}");
            RhinoApp.WriteLine($"Top rail style: {topRailStyleOptions[settings.TopRailStyleIndex]}");
            RhinoApp.WriteLine($"Bottom rail: {bottomRailOptions[settings.BottomRailModeIndex]}");
            RhinoApp.WriteLine($"Post placement: {postPlacementOptions[settings.PostPlacementIndex]}");
            RhinoApp.WriteLine($"Infill style: {infillStyleOptions[settings.InfillStyleIndex]}");
            RhinoApp.WriteLine($"Wall tabs: {(settings.WallTabs ? "Yes" : "No")}");

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
            int postPlacementIndex,
            OptionToggle intermediatePosts,
            OptionDouble intermediatePostSpacing,
            OptionDouble postSpacing,
            int postDistributionIndex,
            OptionToggle wallTabs,
            OptionDouble tabLength,
            int infillStyleIndex,
            OptionDouble infillWidth,
            OptionDouble infillDepth,
            OptionDouble maxInfillSpacing,
            OptionDouble zigZagDiameter,
            OptionDouble zigZagBayLength
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

            settings.PostPlacementIndex = postPlacementIndex;
            settings.IntermediatePosts = postPlacementIndex == 0 && intermediatePosts.CurrentValue;
            settings.IntermediatePostSpacing = intermediatePostSpacing.CurrentValue;
            settings.PostSpacing = postSpacing.CurrentValue;
            settings.PostDistributionIndex = postDistributionIndex;

            settings.WallTabs = wallTabs.CurrentValue;
            settings.TabLength = tabLength.CurrentValue;

            settings.InfillStyleIndex = infillStyleIndex;
            settings.InfillWidth = infillWidth.CurrentValue;
            settings.InfillDepth = infillDepth.CurrentValue;
            settings.MaxInfillSpacing = maxInfillSpacing.CurrentValue;

            settings.ZigZagDiameter = zigZagDiameter.CurrentValue;
            settings.ZigZagBayLength = zigZagBayLength.CurrentValue;

            settings.GroundZ = groundZ;
        }
    }
}
