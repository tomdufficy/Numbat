using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.UI;

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

            _activeDoc = doc;
            _handrailRuns = handrailRuns;
            _settings = new HandrailSettings
            {
                Height = 1100.0,
                TopRailStyleIndex = 1,
                BoxRailDepth = 40.0,
                BoxRailHeight = 20.0,
                TopRailDiameter = 50.0,
                BottomRailModeIndex = 1,
                BottomRailHeight = 100.0,
                SupportFeet = false,
                BayLayoutIndex = 1,
                MaxBayLength = 1200.0,
                Tabs = false,
                TabLength = 75.0,
                InfillStyleIndex = 0,
                InfillWidth = 10.0,
                InfillDepth = 20.0,
                MaxInfillSpacing = 100.0,
                ZigZagDiameter = 10.0,
                ZigZagBayLength = 100.0,
                PanelGap = 50.0,
                PanelFrameWidth = 25.0,
                PanelFrameDepth = 25.0,
                PanelSheetThickness = 5.0,
                PanelTopGap = 50.0,
                PanelBottomGap = 100.0,
                PanelFrameConstructionIndex = 0,
                PreviewDims = true,
                GroundZ = groundZ
            };

            _conduit = new HandrailPreviewConduit
            {
                Enabled = true
            };

            UpdatePreview();

            _dialog = new HandrailDialog(_settings);
            _dialog.HeightChanged += OnHeightChanged;
            _dialog.TopRailChanged += OnTopRailChanged;
            _dialog.BottomRailChanged += OnBottomRailChanged;
            _dialog.BaysChanged += OnBaysChanged;
            _dialog.TabsChanged += OnTabsChanged;
            _dialog.InfillChanged += OnInfillChanged;
            _dialog.PreviewChanged += OnPreviewChanged;
            _dialog.Closed += OnDialogClosed;
            _dialog.Show(_activeDoc);

            return Result.Success;
        }

        private void OnHeightChanged(object sender, EventArgs e)
        {
            if (_dialog == null || _settings == null)
                return;

            _settings.Height = _dialog.HeightStepper.Value;
            UpdatePreview();
        }

        private void OnTopRailChanged(object sender, EventArgs e)
        {
            if (_dialog == null || _settings == null)
                return;

            _settings.TopRailStyleIndex = _dialog.TopRailStyleDropDown.SelectedIndex;
            _settings.BoxRailDepth = _dialog.BoxRailDepthStepper.Value;
            _settings.BoxRailHeight = _dialog.BoxRailHeightStepper.Value;
            _settings.TopRailDiameter = _dialog.TopRailDiameterStepper.Value;
            UpdatePreview();
        }

        private void OnBottomRailChanged(object sender, EventArgs e)
        {
            if (_dialog == null || _settings == null)
                return;

            _settings.BottomRailModeIndex = _dialog.BottomRailModeDropDown.SelectedIndex;
            _settings.BottomRailHeight = _dialog.BottomRailHeightStepper.Value;
            _settings.SupportFeet =
                _settings.BottomRailModeIndex == 2 &&
                _settings.BottomRailHeight > RhinoMath.ZeroTolerance &&
                _dialog.SupportFeetCheckBox.Checked == true;

            UpdatePreview();
        }

        private void OnBaysChanged(object sender, EventArgs e)
        {
            if (_dialog == null || _settings == null)
                return;

            _settings.BayLayoutIndex = _dialog.BayLayoutDropDown.SelectedIndex;
            _settings.MaxBayLength = _dialog.MaxBayLengthStepper.Value;
            UpdatePreview();
        }

        private void OnTabsChanged(object sender, EventArgs e)
        {
            if (_dialog == null || _settings == null)
                return;

            _settings.Tabs = _dialog.TabsCheckBox.Checked == true;
            _settings.TabLength = _dialog.TabLengthStepper.Value;
            UpdatePreview();
        }

        private void OnInfillChanged(object sender, EventArgs e)
        {
            if (_dialog == null || _settings == null)
                return;

            _settings.InfillStyleIndex = _dialog.InfillStyleDropDown.SelectedIndex;
            _settings.InfillWidth = _dialog.InfillWidthStepper.Value;
            _settings.InfillDepth = _dialog.InfillDepthStepper.Value;
            _settings.MaxInfillSpacing = _dialog.MaxInfillSpacingStepper.Value;
            _settings.ZigZagDiameter = _dialog.ZigZagDiameterStepper.Value;
            _settings.ZigZagBayLength = _dialog.ZigZagBayLengthStepper.Value;
            _settings.PanelGap = _dialog.PanelGapStepper.Value;
            _settings.PanelFrameWidth = _dialog.PanelFrameSizeStepper.Value;
            _settings.PanelFrameDepth = _dialog.PanelFrameSizeStepper.Value;
            _settings.PanelSheetThickness = _dialog.PanelSheetThicknessStepper.Value;
            _settings.PanelTopGap = _dialog.PanelTopGapStepper.Value;
            _settings.PanelBottomGap = _dialog.PanelBottomGapStepper.Value;
            _settings.PanelFrameConstructionIndex = _dialog.PanelFrameConstructionDropDown.SelectedIndex;
            UpdatePreview();
        }

        private void OnPreviewChanged(object sender, EventArgs e)
        {
            if (_dialog == null || _settings == null)
                return;

            _settings.PreviewDims = _dialog.PreviewDimensionsCheckBox.Checked == true;
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

                HandrailGenerator.AddGeometryToDocument(_activeDoc, finalGeometry, _settings);
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
                _dialog.TopRailChanged -= OnTopRailChanged;
                _dialog.BottomRailChanged -= OnBottomRailChanged;
                _dialog.BaysChanged -= OnBaysChanged;
                _dialog.TabsChanged -= OnTabsChanged;
                _dialog.InfillChanged -= OnInfillChanged;
                _dialog.PreviewChanged -= OnPreviewChanged;
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
    }
}
