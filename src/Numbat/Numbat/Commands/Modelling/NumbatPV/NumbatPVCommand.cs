using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Numbat.Commands.Modelling.NumbatPV
{
    public class NumbatPVCommand : Command
    {
        public static NumbatPVCommand Instance { get; private set; }

        public NumbatPVCommand()
        {
            Instance = this;
        }

        public override string EnglishName => "nbPV";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var getObjects = new GetObject();
            getObjects.SetCommandPrompt("Select roofs or building volumes");
            getObjects.GeometryFilter = ObjectType.Brep | ObjectType.Surface;

            var getResult = getObjects.GetMultiple(1, 0);
            if (getResult != GetResult.Object)
                return getObjects.CommandResult();

            var objects = new List<ObjRef>();
            for (var i = 0; i < getObjects.ObjectCount; i++)
                objects.Add(getObjects.Object(i));

            var panelWidth = new OptionDouble(1130.0, true, 10.0);
            var panelLength = new OptionDouble(1760.0, true, 10.0);
            var panelThickness = new OptionDouble(40.0, true, 10.0);
            var frameWidth = new OptionDouble(20.0, true, 10.0);
            var frameDepth = new OptionDouble(40.0, true, 10.0);
            var standHeight = new OptionDouble(150.0, true, 10.0);
            var tiltAngle = new OptionDouble(10.0, true, 0.0);
            var panelGap = new OptionDouble(20.0, true, 10.0);
            var rowGap = new OptionDouble(600.0, true, 10.0);
            var parapetMargin = new OptionDouble(1000.0, true, 0.0);
            var rotate90 = new OptionToggle(false, "No", "Yes");
            var fitModeIndex = 0;
            string[] fitModeOptions = { "Strict", "Loose" };

            var parameters = new PVParameters();
            var conduit = new PVPreviewConduit { Enabled = true };
            PVGeometry solution = null;

            try
            {
                while (true)
                {
                    ApplyOptionValues(parameters, panelWidth, panelLength, panelThickness, frameWidth, frameDepth, standHeight, tiltAngle, panelGap, rowGap, parapetMargin, rotate90, fitModeIndex);

                    solution = PVSolver.Solve(objects, parameters, doc.ModelAbsoluteTolerance);
                    conduit.PreviewGeometry = PVBuilder.CreatePreviewGeometry(parameters, solution.PanelTransforms);
                    doc.Views.Redraw();

                    var getOptions = new GetOption();
                    getOptions.SetCommandPrompt("PV options. Press Enter to create PV panels");
                    getOptions.AcceptNothing(true);

                    getOptions.AddOptionDouble("PanelWidth", ref panelWidth);
                    getOptions.AddOptionDouble("PanelLength", ref panelLength);
                    getOptions.AddOptionDouble("PanelThickness", ref panelThickness);
                    getOptions.AddOptionDouble("FrameWidth", ref frameWidth);
                    getOptions.AddOptionDouble("FrameDepth", ref frameDepth);
                    getOptions.AddOptionDouble("StandHeight", ref standHeight);
                    getOptions.AddOptionDouble("TiltAngle", ref tiltAngle);
                    getOptions.AddOptionDouble("PanelGap", ref panelGap);
                    getOptions.AddOptionDouble("RowGap", ref rowGap);
                    getOptions.AddOptionDouble("ParapetMargin", ref parapetMargin);
                    getOptions.AddOptionToggle("Rotate90", ref rotate90);
                    getOptions.AddOptionList("FitMode", fitModeOptions, fitModeIndex);

                    var result = getOptions.Get();
                    if (result == GetResult.Nothing)
                        break;
                    if (result == GetResult.Cancel)
                        return Result.Cancel;
                    if (result == GetResult.Option)
                    {
                        var option = getOptions.Option();
                        if (option != null && option.EnglishName == "FitMode")
                            fitModeIndex = option.CurrentListOptionIndex;
                    }
                }
            }
            finally
            {
                conduit.Enabled = false;
                doc.Views.Redraw();
            }

            ApplyOptionValues(parameters, panelWidth, panelLength, panelThickness, frameWidth, frameDepth, standHeight, tiltAngle, panelGap, rowGap, parapetMargin, rotate90, fitModeIndex);
            solution = PVSolver.Solve(objects, parameters, doc.ModelAbsoluteTolerance);

            if (solution.PanelTransforms.Count == 0)
            {
                RhinoApp.WriteLine("nbPV did not find enough roof area for any panels.");
                RhinoApp.WriteLine($"Skipped objects: {solution.SkippedCount}");
                return Result.Nothing;
            }

            PVBake.AddGeometryToDocument(doc, solution, parameters);
            doc.Views.Redraw();

            RhinoApp.WriteLine($"nbPV created {solution.PanelTransforms.Count} panel instances on {solution.RoofCount} roof(s).");
            if (solution.SkippedCount > 0)
                RhinoApp.WriteLine($"Skipped {solution.SkippedCount} object(s) with no planar upward roof face.");

            return Result.Success;
        }

        private static void ApplyOptionValues(
            PVParameters parameters,
            OptionDouble panelWidth,
            OptionDouble panelLength,
            OptionDouble panelThickness,
            OptionDouble frameWidth,
            OptionDouble frameDepth,
            OptionDouble standHeight,
            OptionDouble tiltAngle,
            OptionDouble panelGap,
            OptionDouble rowGap,
            OptionDouble parapetMargin,
            OptionToggle rotate90,
            int fitModeIndex)
        {
            parameters.PanelWidth = panelWidth.CurrentValue;
            parameters.PanelLength = panelLength.CurrentValue;
            parameters.PanelThickness = panelThickness.CurrentValue;
            parameters.FrameWidth = frameWidth.CurrentValue;
            parameters.FrameDepth = frameDepth.CurrentValue;
            parameters.StandHeight = standHeight.CurrentValue;
            parameters.TiltAngleDegrees = tiltAngle.CurrentValue;
            parameters.PanelGap = panelGap.CurrentValue;
            parameters.RowGap = rowGap.CurrentValue;
            parameters.ParapetMargin = parapetMargin.CurrentValue;
            parameters.Rotate90 = rotate90.CurrentValue;
            parameters.FitMode = fitModeIndex == 1 ? PVFitMode.Loose : PVFitMode.Strict;
            parameters.Clamp();
        }
    }
}
