using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Numbat.Commands.Modelling.NumbatHandrail
{
    public class NumbatHandrailCommand : Command
    {
        public static NumbatHandrailCommand Instance { get; private set; }

        private HandrailDialog _dialog;
        private HandrailPreviewConduit _conduit;
        private RhinoDoc _activeDoc;
        private List<Curve> _handrailRuns;
        private HandrailSettings _settings;

        public NumbatHandrailCommand()
        {
            Instance = this;
        }

        public override string EnglishName => "nbHandrail";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (_dialog != null)
            {
                _dialog.Focus();
                return Result.Success;
            }

            var pathResult = GetHandrailRuns(doc, out var handrailRuns, out var groundZ);

            if (pathResult != Result.Success)
                return pathResult;

            var height = new OptionDouble(1100.0, true, 100.0);
            var topRailStyleIndex = 1;
            var boxRailDepth = new OptionDouble(40.0, true, 1.0);
            var boxRailHeight = new OptionDouble(20.0, true, 1.0);
            var topRailDiameter = new OptionDouble(50.0, true, 1.0);
            var bottomRailModeIndex = 1;
            var bottomRailHeight = new OptionDouble(100.0, true, 0.0);
            var supportFeet = new OptionToggle(true, "No", "Yes");
            var bayLayoutIndex = 1;
            var maxBayLength = new OptionDouble(1200.0, true, 100.0);
            var tabs = new OptionToggle(false, "No", "Yes");
            var tabLength = new OptionDouble(75.0, true, 1.0);
            var infillStyleIndex = 0;
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
            var previewDims = new OptionToggle(true, "No", "Yes");

            _activeDoc = doc;
            _handrailRuns = handrailRuns;
            _settings = new HandrailSettings();

            ApplyOptionValuesToSettings(
                _settings,
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

            _conduit = new HandrailPreviewConduit
            {
                Enabled = true
            };

            UpdatePreview();

            _dialog = new HandrailDialog(_settings);
            _dialog.HeightChanged += OnHeightChanged;
            _dialog.Closed += OnDialogClosed;
            _dialog.Show();

            return Result.Success;
        }

        private void OnHeightChanged(object sender, EventArgs e)
        {
            if (_dialog == null || _settings == null)
                return;

            _settings.Height = _dialog.HeightStepper.Value;
            UpdatePreview();
        }

        private void OnDialogClosed(object sender, EventArgs e)
        {
            var accepted = _dialog != null && _dialog.Accepted;

            if (accepted && _activeDoc != null && _handrailRuns != null && _settings != null)
            {
                var finalGeometry = HandrailGenerator.CreateHandrailGeometry(
                    _handrailRuns,
                    _settings,
                    _activeDoc.ModelAbsoluteTolerance
                );

                HandrailGenerator.AddGeometryToDocument(_activeDoc, finalGeometry);
                RhinoApp.WriteLine("nbHandrail created.");
                RhinoApp.WriteLine($"Height: {_settings.Height}");
            }

            CleanupPreview();
        }

        private void UpdatePreview()
        {
            if (_activeDoc == null || _handrailRuns == null || _settings == null || _conduit == null)
                return;

            var previewGeometry = HandrailGenerator.CreateHandrailGeometry(
                _handrailRuns,
                _settings,
                _activeDoc.ModelAbsoluteTolerance
            );

            _conduit.PreviewBreps = previewGeometry.AllBreps();
            _conduit.PreviewLabels = previewGeometry.PreviewLabels;
            _conduit.PreviewLines = previewGeometry.PreviewLines;
            _activeDoc.Views.Redraw();
        }

        private void CleanupPreview()
        {
            if (_dialog != null)
            {
                _dialog.HeightChanged -= OnHeightChanged;
                _dialog.Closed -= OnDialogClosed;
            }

            if (_conduit != null)
                _conduit.Enabled = false;

            if (_activeDoc != null)
                _activeDoc.Views.Redraw();

            _dialog = null;
            _conduit = null;
            _activeDoc = null;
            _handrailRuns = null;
            _settings = null;
        }

        private static Result GetHandrailRuns(RhinoDoc doc, out List<Curve> runs, out double groundZ)
        {
            runs = new List<Curve>();
            groundZ = 0.0;

            var firstPointGetter = new GetPoint();
            firstPointGetter.SetCommandPrompt("Pick handrail start point. Press Enter for default 5000 mm railing");
            firstPointGetter.AcceptNothing(true);

            var firstResult = firstPointGetter.Get();

            if (firstResult == GetResult.Nothing)
            {
                runs.Add(new LineCurve(
                    new Point3d(0.0, 0.0, 0.0),
                    new Point3d(5000.0, 0.0, 0.0)
                ));

                return Result.Success;
            }

            if (firstPointGetter.CommandResult() != Result.Success)
                return firstPointGetter.CommandResult();

            var points = new List<Point3d> { firstPointGetter.Point() };
            groundZ = points[0].Z;
            var previewGroundZ = groundZ;

            while (true)
            {
                var pointGetter = new GetPoint();
                pointGetter.SetCommandPrompt("Pick next handrail point. Press Enter to finish");
                pointGetter.SetBasePoint(points[points.Count - 1], true);
                pointGetter.DrawLineFromPoint(points[points.Count - 1], true);
                pointGetter.Constrain(new Plane(new Point3d(0.0, 0.0, groundZ), Vector3d.ZAxis), false);
                pointGetter.AcceptNothing(points.Count >= 2);
                pointGetter.DynamicDraw += (sender, e) =>
                {
                    var previewColor = System.Drawing.Color.FromArgb(246, 217, 245);

                    for (var i = 0; i < points.Count - 1; i++)
                        e.Display.DrawLine(points[i], points[i + 1], previewColor, 2);

                    var currentPoint = e.CurrentPoint;
                    currentPoint.Z = previewGroundZ;
                    e.Display.DrawLine(points[points.Count - 1], currentPoint, previewColor, 2);
                };

                var result = pointGetter.Get();

                if (result == GetResult.Nothing)
                    break;

                if (pointGetter.CommandResult() != Result.Success)
                    return pointGetter.CommandResult();

                var pickedPoint = pointGetter.Point();
                pickedPoint.Z = groundZ;

                if (pickedPoint.DistanceTo(points[points.Count - 1]) <= doc.ModelAbsoluteTolerance)
                {
                    RhinoApp.WriteLine("The next handrail point must be different from the previous point.");
                    continue;
                }

                points.Add(pickedPoint);
            }

            for (var i = 0; i < points.Count - 1; i++)
                runs.Add(new LineCurve(points[i], points[i + 1]));

            return runs.Count > 0 ? Result.Success : Result.Cancel;
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
