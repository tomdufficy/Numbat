using System;
using System.Drawing;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Numbat.Commands.Modelling.NumbatStair.Spiral
{
    public class NumbatStairSpiralCommand : Command
    {
        public static NumbatStairSpiralCommand Instance { get; private set; }

        public NumbatStairSpiralCommand()
        {
            Instance = this;
        }

        public override string EnglishName => "nbStairSpiral";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            return RunStairSpiral(doc);
        }

        public static Result RunStairSpiral(RhinoDoc doc)
        {
            var getBasePoint = new GetPoint();
            getBasePoint.SetCommandPrompt("Pick spiral stair base centre point. Press Enter for 0,0,0");
            getBasePoint.AcceptNothing(true);
            var baseResult = getBasePoint.Get();

            if (baseResult == GetResult.Cancel)
                return Result.Cancel;

            if (getBasePoint.CommandResult() != Result.Success && baseResult != GetResult.Nothing)
                return getBasePoint.CommandResult();

            var baseCenter = baseResult == GetResult.Nothing ? Point3d.Origin : getBasePoint.Point();

            var getDirectionPoint = new GetPoint();
            getDirectionPoint.SetCommandPrompt("Pick start direction. Press Enter for world X");
            getDirectionPoint.SetBasePoint(baseCenter, true);
            getDirectionPoint.DrawLineFromPoint(baseCenter, true);
            getDirectionPoint.AcceptNothing(true);
            getDirectionPoint.DynamicDraw += (sender, e) => DrawDirectionArrow(e.Display, baseCenter, e.CurrentPoint);
            var directionResult = getDirectionPoint.Get();

            if (directionResult == GetResult.Cancel)
                return Result.Cancel;

            if (getDirectionPoint.CommandResult() != Result.Success && directionResult != GetResult.Nothing)
                return getDirectionPoint.CommandResult();

            var directionPoint = directionResult == GetResult.Nothing
                ? baseCenter + new Vector3d(1100.0, 0.0, 0.0)
                : getDirectionPoint.Point();

            var directionVector = directionPoint - baseCenter;
            directionVector.Z = 0.0;

            if (!directionVector.Unitize())
            {
                RhinoApp.WriteLine("Start direction was too short.");
                return Result.Failure;
            }

            var startAngle = Math.Atan2(directionVector.Y, directionVector.X);

            var radius = new OptionDouble(1200.0, true, 500.0);
            var floorHeight = new OptionDouble(3000.0, true, 100.0);
            var maxRiser = new OptionDouble(180.0, true, 50.0);
            var columnDiameter = new OptionDouble(120.0, true, 20.0);
            var handrailHeight = new OptionDouble(900.0, true, 100.0);
            var handrailDiameter = new OptionDouble(35.0, true, 5.0);
            var balusterDiameter = new OptionDouble(16.0, true, 5.0);
            var closedSkinThickness = new OptionDouble(0.0, true, -100000.0);
            var splitClosedSkin = new OptionToggle(false, "No", "Yes");
            var soffitThickness = new OptionDouble(5.0, true, 0.0);

            var stairModeIndex = 0;
            string[] stairModeOptions = { "Open", "Closed" };

            var endDirectionIndex = 0;
            string[] endDirectionOptions = { "Same", "Right90", "Left90", "Opposite" };

            var directionIndex = 0;
            string[] directionOptions = { "Clockwise", "CounterClockwise" };

            var parameters = new StairSpiralParameters
            {
                BaseCenter = baseCenter,
                StartAngleRadians = startAngle
            };

            var conduit = new StairSpiralPreviewConduit { Enabled = true };

            try
            {
                while (true)
                {
                    ApplyOptionValues(parameters, radius, floorHeight, maxRiser, columnDiameter, handrailHeight, handrailDiameter, balusterDiameter, closedSkinThickness, splitClosedSkin, soffitThickness, stairModeIndex, endDirectionIndex, directionIndex);

                    var solution = StairSpiralSolver.Solve(parameters);
                    var previewGeometry = StairSpiralBuilder.Build(solution, doc.ModelAbsoluteTolerance);

                    conduit.PreviewGeometry = previewGeometry.AllGeometry();
                    conduit.PreviewLabels = previewGeometry.PreviewLabels;
                    conduit.PreviewLines = previewGeometry.PreviewLines;

                    doc.Views.Redraw();

                    var getOptions = new GetOption();
                    getOptions.SetCommandPrompt("Spiral stair options. Press Enter to create stair");
                    getOptions.AcceptNothing(true);

                    getOptions.AddOptionList("StairType", stairModeOptions, stairModeIndex);
                    getOptions.AddOptionDouble("Radius", ref radius);
                    getOptions.AddOptionDouble("FloorHeight", ref floorHeight);
                    getOptions.AddOptionDouble("MaxRiser", ref maxRiser);
                    getOptions.AddOptionList("EndDirection", endDirectionOptions, endDirectionIndex);
                    getOptions.AddOptionList("Direction", directionOptions, directionIndex);
                    getOptions.AddOptionDouble("ColumnDiameter", ref columnDiameter);
                    getOptions.AddOptionDouble("HandrailHeight", ref handrailHeight);

                    if ((StairSpiralMode)stairModeIndex == StairSpiralMode.Open)
                    {
                        getOptions.AddOptionDouble("HandrailDiameter", ref handrailDiameter);
                        getOptions.AddOptionDouble("BalusterDiameter", ref balusterDiameter);
                    }

                    if ((StairSpiralMode)stairModeIndex == StairSpiralMode.Closed)
                    {
                        getOptions.AddOptionDouble("SkinThickness", ref closedSkinThickness);
                        getOptions.AddOptionDouble("SoffitThickness", ref soffitThickness);
                        getOptions.AddOptionToggle("SplitSkin", ref splitClosedSkin);
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
                            if (option.EnglishName == "StairType")
                                stairModeIndex = option.CurrentListOptionIndex;
                            else if (option.EnglishName == "EndDirection")
                                endDirectionIndex = option.CurrentListOptionIndex;
                            else if (option.EnglishName == "Direction")
                                directionIndex = option.CurrentListOptionIndex;
                        }
                    }
                }
            }
            finally
            {
                conduit.Enabled = false;
                doc.Views.Redraw();
            }

            ApplyOptionValues(parameters, radius, floorHeight, maxRiser, columnDiameter, handrailHeight, handrailDiameter, balusterDiameter, closedSkinThickness, splitClosedSkin, soffitThickness, stairModeIndex, endDirectionIndex, directionIndex);

            var finalSolution = StairSpiralSolver.Solve(parameters);
            var finalGeometry = StairSpiralBuilder.Build(finalSolution, doc.ModelAbsoluteTolerance);

            StairSpiralBake.AddGeometryToDocument(doc, finalGeometry);

            doc.Views.Redraw();

            RhinoApp.WriteLine("nbStairSpiral created.");
            RhinoApp.WriteLine($"Radius: {parameters.Radius:0} mm");
            RhinoApp.WriteLine($"Floor height: {parameters.FloorHeight:0} mm");
            RhinoApp.WriteLine($"Risers: {finalSolution.RiserCount} @ {finalSolution.ActualRiserHeight:0.0} mm");
            RhinoApp.WriteLine($"Total rotation: {finalSolution.TotalRotationDegrees:0} degrees");

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
            StairSpiralParameters parameters,
            OptionDouble radius,
            OptionDouble floorHeight,
            OptionDouble maxRiser,
            OptionDouble columnDiameter,
            OptionDouble handrailHeight,
            OptionDouble handrailDiameter,
            OptionDouble balusterDiameter,
            OptionDouble closedSkinThickness,
            OptionToggle splitClosedSkin,
            OptionDouble soffitThickness,
            int stairModeIndex,
            int endDirectionIndex,
            int directionIndex)
        {
            parameters.Radius = radius.CurrentValue;
            parameters.FloorHeight = floorHeight.CurrentValue;
            parameters.MaxRiserHeight = maxRiser.CurrentValue;
            parameters.ColumnDiameter = columnDiameter.CurrentValue;
            parameters.HandrailHeight = handrailHeight.CurrentValue;
            parameters.HandrailDiameter = handrailDiameter.CurrentValue;
            parameters.BalusterDiameter = balusterDiameter.CurrentValue;
            parameters.ClosedSkinThickness = closedSkinThickness.CurrentValue;
            parameters.SplitClosedSkin = splitClosedSkin.CurrentValue;
            parameters.SoffitThickness = soffitThickness.CurrentValue;
            parameters.Mode = (StairSpiralMode)stairModeIndex;
            parameters.EndDirection = (StairSpiralEndDirection)endDirectionIndex;
            parameters.Direction = (StairSpiralDirection)directionIndex;
        }
    }
}
