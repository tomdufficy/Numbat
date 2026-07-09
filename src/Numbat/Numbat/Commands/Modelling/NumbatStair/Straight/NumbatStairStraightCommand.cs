using System;
using System.Drawing;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Numbat.Commands.Modelling.NumbatStair.Straight
{
    public class NumbatStairStraightCommand : Command
    {
        public static NumbatStairStraightCommand Instance { get; private set; }

        public NumbatStairStraightCommand()
        {
            Instance = this;
        }

        public override string EnglishName => "nbStairStraight";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            return RunStairStraight(doc);
        }

        public static Result RunStairStraight(RhinoDoc doc)
        {
            var getStartPoint = new GetPoint();
            getStartPoint.SetCommandPrompt("Pick straight stair start point. Press Enter for 0,0,0");
            getStartPoint.AcceptNothing(true);
            var startResult = getStartPoint.Get();

            if (startResult == GetResult.Cancel)
                return Result.Cancel;

            if (getStartPoint.CommandResult() != Result.Success && startResult != GetResult.Nothing)
                return getStartPoint.CommandResult();

            var startPoint = startResult == GetResult.Nothing ? Point3d.Origin : getStartPoint.Point();

            var getDirectionPoint = new GetPoint();
            getDirectionPoint.SetCommandPrompt("Pick stair width direction. Press Enter for 1100 mm along world X");
            getDirectionPoint.SetBasePoint(startPoint, true);
            getDirectionPoint.DrawLineFromPoint(startPoint, true);
            getDirectionPoint.AcceptNothing(true);
            getDirectionPoint.DynamicDraw += (sender, e) => DrawDirectionArrow(e.Display, startPoint, e.CurrentPoint);
            var directionResult = getDirectionPoint.Get();

            if (directionResult == GetResult.Cancel)
                return Result.Cancel;

            if (getDirectionPoint.CommandResult() != Result.Success && directionResult != GetResult.Nothing)
                return getDirectionPoint.CommandResult();

            var directionPoint = directionResult == GetResult.Nothing
                ? startPoint + new Vector3d(1100.0, 0.0, 0.0)
                : getDirectionPoint.Point();

            var widthVector = directionPoint - startPoint;
            widthVector.Z = 0.0;

            var pickedWidth = widthVector.Length;
            if (pickedWidth < 1.0 || !widthVector.Unitize())
            {
                RhinoApp.WriteLine("Stair width direction was too short.");
                return Result.Failure;
            }

            var direction = new Vector3d(widthVector.Y, -widthVector.X, 0.0);
            if (!direction.Unitize())
            {
                RhinoApp.WriteLine("Stair direction was too short.");
                return Result.Failure;
            }

            var width = new OptionDouble(pickedWidth, true, 100.0);
            var floorHeight = new OptionDouble(3000.0, true, 100.0);
            var maxRiser = new OptionDouble(180.0, true, 50.0);
            var maxStepsBeforeLanding = new OptionInteger(12, true, 1);
            var treadDepth = new OptionDouble(280.0, true, 100.0);
            var treadThickness = new OptionDouble(30.0, true, 1.0);
            var nosing = new OptionDouble(15.0, true, 0.0);
            var landingDepth = new OptionDouble(Math.Max(1100.0, pickedWidth), true, 0.0);
            var modeIndex = 0;
            var landingModeIndex = 0;
            var switchback = false;
            var switchbackSideIndex = 0;
            string[] modeOptions = { "Monolithic", "SteelStringers" };
            string[] landingModeOptions = { "Middle", "MaxRisers" };

            var parameters = new StairStraightParameters
            {
                StartPoint = startPoint + ((directionPoint - startPoint) * 0.5),
                Direction = direction
            };

            var conduit = new StairStraightPreviewConduit { Enabled = true };

            try
            {
                while (true)
                {
                    ApplyOptionValues(parameters, width, floorHeight, maxRiser, maxStepsBeforeLanding, treadDepth, treadThickness, nosing, landingDepth, modeIndex, landingModeIndex, switchback, switchbackSideIndex);

                    var solution = StairStraightSolver.Solve(parameters);
                    var previewGeometry = StairStraightBuilder.Build(solution, doc.ModelAbsoluteTolerance);

                    conduit.PreviewGeometry = previewGeometry.AllGeometry();
                    conduit.PreviewLabels = previewGeometry.PreviewLabels;
                    conduit.PreviewLines = previewGeometry.PreviewLines;

                    doc.Views.Redraw();

                    var getOptions = new GetOption();
                    getOptions.SetCommandPrompt("Straight stair options. Press Enter to create stair");
                    getOptions.AcceptNothing(true);

                    getOptions.AddOptionList("Construction", modeOptions, modeIndex);
                    getOptions.AddOptionDouble("Width", ref width);
                    getOptions.AddOptionDouble("FloorHeight", ref floorHeight);
                    getOptions.AddOptionDouble("MaxRiser", ref maxRiser);
                    getOptions.AddOptionList("LandingMode", landingModeOptions, landingModeIndex);
                    getOptions.AddOption("Switchback", switchback ? "Yes" : "No");
                    if (switchback)
                        getOptions.AddOption("FlipSwitchback", switchbackSideIndex == 0 ? "Right" : "Left");
                    if (landingModeIndex == (int)StairStraightLandingMode.MaxRisers)
                        getOptions.AddOptionInteger("MaxStepsBeforeLanding", ref maxStepsBeforeLanding);
                    getOptions.AddOptionDouble("TreadDepth", ref treadDepth);
                    getOptions.AddOptionDouble("TreadThickness", ref treadThickness);
                    getOptions.AddOptionDouble("Nosing", ref nosing);
                    getOptions.AddOptionDouble("LandingDepth", ref landingDepth);

                    var result = getOptions.Get();

                    if (result == GetResult.Nothing)
                        break;

                    if (result == GetResult.Cancel)
                        return Result.Cancel;

                    if (result == GetResult.Option)
                    {
                        var option = getOptions.Option();
                        if (option != null && option.EnglishName == "Construction")
                            modeIndex = option.CurrentListOptionIndex;
                        else if (option != null && option.EnglishName == "LandingMode")
                            landingModeIndex = option.CurrentListOptionIndex;
                        else if (option != null && option.EnglishName == "Switchback")
                            switchback = !switchback;
                        else if (option != null && option.EnglishName == "FlipSwitchback")
                            switchbackSideIndex = switchbackSideIndex == 0 ? 1 : 0;
                    }
                }
            }
            finally
            {
                conduit.Enabled = false;
                doc.Views.Redraw();
            }

            ApplyOptionValues(parameters, width, floorHeight, maxRiser, maxStepsBeforeLanding, treadDepth, treadThickness, nosing, landingDepth, modeIndex, landingModeIndex, switchback, switchbackSideIndex);

            var finalSolution = StairStraightSolver.Solve(parameters);
            var finalGeometry = StairStraightBuilder.Build(finalSolution, doc.ModelAbsoluteTolerance);

            StairStraightBake.AddGeometryToDocument(doc, finalGeometry);

            doc.Views.Redraw();

            RhinoApp.WriteLine("nbStairStraight created.");
            RhinoApp.WriteLine($"Width: {parameters.Width:0} mm");
            RhinoApp.WriteLine($"Floor height: {parameters.FloorHeight:0} mm");
            RhinoApp.WriteLine($"Risers: {finalSolution.RiserCount} @ {finalSolution.ActualRiserHeight:0.0} mm");
            RhinoApp.WriteLine($"Flights: {finalSolution.FlightCount}");
            RhinoApp.WriteLine($"Landings: {finalSolution.LandingCount}");
            RhinoApp.WriteLine($"Construction: {parameters.Mode}");
            RhinoApp.WriteLine($"Landing mode: {parameters.LandingMode}");

            if (!string.IsNullOrWhiteSpace(finalSolution.Warning))
                RhinoApp.WriteLine($"Warning: {finalSolution.Warning}");

            return Result.Success;
        }

        private static void DrawDirectionArrow(Rhino.Display.DisplayPipeline display, Point3d startPoint, Point3d currentPoint)
        {
            var widthVector = currentPoint - startPoint;
            widthVector.Z = 0.0;

            var widthLength = widthVector.Length;
            if (widthLength < 1.0)
                return;

            var lineColour = Color.FromArgb(246, 217, 245);
            display.DrawLine(startPoint, currentPoint, lineColour, 2);

            var arrowDirection = new Vector3d(widthVector.Y, -widthVector.X, 0.0);
            if (!arrowDirection.Unitize())
                return;

            var arrowLength = Math.Max(250.0, Math.Min(widthLength * 0.75, 1200.0));
            var arrowStart = startPoint + (widthVector * 0.5);
            var arrowEnd = arrowStart + (arrowDirection * arrowLength);

            display.DrawLine(arrowStart, arrowEnd, lineColour, 3);

            var headLength = Math.Max(80.0, Math.Min(arrowLength * 0.25, 250.0));
            var sideDirection = widthVector;
            if (!sideDirection.Unitize())
                return;

            var headPointA = arrowEnd - (arrowDirection * headLength) + (sideDirection * headLength * 0.45);
            var headPointB = arrowEnd - (arrowDirection * headLength) - (sideDirection * headLength * 0.45);

            display.DrawLine(arrowEnd, headPointA, lineColour, 3);
            display.DrawLine(arrowEnd, headPointB, lineColour, 3);
        }

        private static void ApplyOptionValues(
            StairStraightParameters parameters,
            OptionDouble width,
            OptionDouble floorHeight,
            OptionDouble maxRiser,
            OptionInteger maxStepsBeforeLanding,
            OptionDouble treadDepth,
            OptionDouble treadThickness,
            OptionDouble nosing,
            OptionDouble landingDepth,
            int modeIndex,
            int landingModeIndex,
            bool switchback,
            int switchbackSideIndex)
        {
            parameters.Width = width.CurrentValue;
            parameters.FloorHeight = floorHeight.CurrentValue;
            parameters.MaxRiserHeight = maxRiser.CurrentValue;
            parameters.MaxStepsBeforeLanding = maxStepsBeforeLanding.CurrentValue;
            parameters.TreadDepth = treadDepth.CurrentValue;
            parameters.TreadThickness = treadThickness.CurrentValue;
            parameters.Nosing = nosing.CurrentValue;
            parameters.LandingDepth = landingDepth.CurrentValue;
            parameters.Mode = (StairStraightMode)modeIndex;
            parameters.LandingMode = (StairStraightLandingMode)landingModeIndex;
            parameters.Switchback = switchback;
            parameters.SwitchbackSide = (StairStraightSwitchbackSide)switchbackSideIndex;
        }
    }
}
