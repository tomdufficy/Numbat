using System;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Numbat.Commands.Modelling.NumbatSpiralStair
{
    public class NumbatSpiralStairCommand : Command
    {
        public static NumbatSpiralStairCommand Instance { get; private set; }

        public NumbatSpiralStairCommand()
        {
            Instance = this;
        }

        public override string EnglishName => "nbSpiralStair";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
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

            var parameters = new SpiralStairParameters
            {
                BaseCenter = baseCenter,
                StartAngleRadians = startAngle
            };

            var conduit = new SpiralStairPreviewConduit { Enabled = true };

            try
            {
                while (true)
                {
                    ApplyOptionValues(parameters, radius, floorHeight, maxRiser, columnDiameter, handrailHeight, handrailDiameter, balusterDiameter, closedSkinThickness, splitClosedSkin, soffitThickness, stairModeIndex, endDirectionIndex, directionIndex);

                    var solution = SpiralStairSolver.Solve(parameters);
                    var previewGeometry = SpiralStairBuilder.Build(solution, doc.ModelAbsoluteTolerance);

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

                    if ((SpiralStairMode)stairModeIndex == SpiralStairMode.Open)
                    {
                        getOptions.AddOptionDouble("HandrailDiameter", ref handrailDiameter);
                        getOptions.AddOptionDouble("BalusterDiameter", ref balusterDiameter);
                    }

                    if ((SpiralStairMode)stairModeIndex == SpiralStairMode.Closed)
                    {
                        getOptions.AddOptionDouble("SkinThickness", ref closedSkinThickness);
                        getOptions.AddOptionDouble("SoffitThickness", ref soffitThickness);
                        if (Math.Abs(closedSkinThickness.CurrentValue) > 0.001)
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

            var finalSolution = SpiralStairSolver.Solve(parameters);
            var finalGeometry = SpiralStairBuilder.Build(finalSolution, doc.ModelAbsoluteTolerance);

            SpiralStairBake.AddGeometryToDocument(doc, finalGeometry);

            doc.Views.Redraw();

            RhinoApp.WriteLine("nbSpiralStair created.");
            RhinoApp.WriteLine($"Radius: {parameters.Radius:0} mm");
            RhinoApp.WriteLine($"Floor height: {parameters.FloorHeight:0} mm");
            RhinoApp.WriteLine($"Risers: {finalSolution.RiserCount} @ {finalSolution.ActualRiserHeight:0.0} mm");
            RhinoApp.WriteLine($"Total rotation: {finalSolution.TotalRotationDegrees:0} degrees");

            if (!string.IsNullOrWhiteSpace(finalSolution.Warning))
                RhinoApp.WriteLine($"Warning: {finalSolution.Warning}");

            return Result.Success;
        }

        private static void ApplyOptionValues(
            SpiralStairParameters parameters,
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
            parameters.SplitClosedSkin = splitClosedSkin.CurrentValue && Math.Abs(closedSkinThickness.CurrentValue) > 0.001;
            parameters.SoffitThickness = soffitThickness.CurrentValue;
            parameters.Mode = (SpiralStairMode)stairModeIndex;
            parameters.EndDirection = (SpiralStairEndDirection)endDirectionIndex;
            parameters.Direction = (SpiralStairDirection)directionIndex;
        }
    }
}
