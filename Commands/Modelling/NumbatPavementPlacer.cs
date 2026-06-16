using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Numbat.Commands.Modelling
{
    public class NumbatPavementPlacerCommand : Command
    {
        public static NumbatPavementPlacerCommand Instance { get; private set; }

        public NumbatPavementPlacerCommand()
        {
            Instance = this;
        }

        public override string EnglishName => "nbPavementPlacer";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var gp = new GetObject();
            gp.SetCommandPrompt("Select a single closed curve representing one paver");
            gp.GeometryFilter = ObjectType.Curve;
            gp.EnablePreSelect(true, true);
            gp.Get();

            if (gp.CommandResult() != Result.Success)
                return gp.CommandResult();

            var paverCurve = gp.Object(0).Curve()?.DuplicateCurve();

            if (paverCurve == null || !paverCurve.IsClosed)
            {
                RhinoApp.WriteLine("The paver must be a single closed curve.");
                return Result.Failure;
            }

            var gc = new GetObject();
            gc.SetCommandPrompt("Select one or more target curves for pavement placement");
            gc.GeometryFilter = ObjectType.Curve;
            gc.EnablePreSelect(false, true);
            gc.GetMultiple(1, 0);

            if (gc.CommandResult() != Result.Success)
                return gc.CommandResult();

            var targetCurves = new List<Curve>();

            for (var i = 0; i < gc.ObjectCount; i++)
            {
                var curve = gc.Object(i).Curve()?.DuplicateCurve();

                if (curve != null)
                    targetCurves.Add(curve);
            }

            if (targetCurves.Count == 0)
                return Result.Failure;

            var paverSizes = GetPaverSizes(paverCurve);
            var defaultGap = 5.0;
            var defaultPatternOffset = paverSizes.AcrossSize + defaultGap;

            var gap = new OptionDouble(defaultGap, true, 0.0);
            var expandPattern = new OptionInteger(0, true, 0);
            var patternOffset = new OptionDouble(defaultPatternOffset, true, 0.0);
            var flipPatternDirection = new OptionToggle(false, "Original", "Flipped");

            var randomStartPoint = new OptionToggle(false, "Original", "Random");
            var randomScale = new OptionToggle(false, "No", "Yes");
            var maxScalePercent = new OptionDouble(2.0, true, 0.0);
            var randomRotation = new OptionToggle(false, "No", "Yes");
            var maxRotationDegrees = new OptionDouble(2.0, true, 0.0);

            var sideIndex = 0;
            string[] sideOptions = { "Center", "Left", "Right" };

            var startIndex = 0;
            string[] startOptions = { "CurveStart", "HalfGapInset" };

            var settings = new PavementSettings();

            var conduit = new PavementPreviewConduit();
            conduit.Enabled = true;

            try
            {
                while (true)
                {
                    settings.Gap = gap.CurrentValue;
                    settings.ExpandPatternCount = expandPattern.CurrentValue;
                    settings.PatternOffset = patternOffset.CurrentValue;
                    settings.FlipPatternDirection = flipPatternDirection.CurrentValue;
                    settings.SideIndex = sideIndex;
                    settings.StartIndex = startIndex;
                    settings.RandomStartPoint = randomStartPoint.CurrentValue;
                    settings.RandomScale = randomScale.CurrentValue;
                    settings.MaxScalePercent = maxScalePercent.CurrentValue;
                    settings.RandomRotation = randomRotation.CurrentValue;
                    settings.MaxRotationDegrees = maxRotationDegrees.CurrentValue;

                    var placementCurves = CreatePlacementCurves(targetCurves, settings, doc.ModelAbsoluteTolerance);
                    conduit.PreviewCurves = CreatePaversForTargets(paverCurve, placementCurves, settings);
                    doc.Views.Redraw();

                    var getOptions = new GetOption();
                    getOptions.SetCommandPrompt("Pavement placer options. Press Enter to place pavers");
                    getOptions.AcceptNothing(true);

                    getOptions.AddOptionDouble("Gap", ref gap);
                    getOptions.AddOptionInteger("ExpandPattern", ref expandPattern);
                    getOptions.AddOptionDouble("PatternOffset", ref patternOffset);
                    getOptions.AddOptionToggle("PatternDirection", ref flipPatternDirection);
                    getOptions.AddOptionList("Side", sideOptions, sideIndex);
                    getOptions.AddOptionList("Start", startOptions, startIndex);
                    getOptions.AddOptionToggle("RandomStartPoint", ref randomStartPoint);
                    getOptions.AddOptionToggle("RandomScale", ref randomScale);
                    getOptions.AddOptionDouble("MaxScalePercent", ref maxScalePercent);
                    getOptions.AddOptionToggle("RandomRotation", ref randomRotation);
                    getOptions.AddOptionDouble("MaxRotationDegrees", ref maxRotationDegrees);

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
                            if (option.Index == 5)
                                sideIndex = option.CurrentListOptionIndex;

                            if (option.Index == 6)
                                startIndex = option.CurrentListOptionIndex;
                        }
                    }
                }
            }
            finally
            {
                conduit.Enabled = false;
                doc.Views.Redraw();
            }

            settings.Gap = gap.CurrentValue;
            settings.ExpandPatternCount = expandPattern.CurrentValue;
            settings.PatternOffset = patternOffset.CurrentValue;
            settings.FlipPatternDirection = flipPatternDirection.CurrentValue;
            settings.SideIndex = sideIndex;
            settings.StartIndex = startIndex;
            settings.RandomStartPoint = randomStartPoint.CurrentValue;
            settings.RandomScale = randomScale.CurrentValue;
            settings.MaxScalePercent = maxScalePercent.CurrentValue;
            settings.RandomRotation = randomRotation.CurrentValue;
            settings.MaxRotationDegrees = maxRotationDegrees.CurrentValue;

            var finalPlacementCurves = CreatePlacementCurves(targetCurves, settings, doc.ModelAbsoluteTolerance);
            var finalPavers = CreatePaversForTargets(paverCurve, finalPlacementCurves, settings);

            foreach (var curve in finalPavers)
                doc.Objects.AddCurve(curve);

            doc.Views.Redraw();

            RhinoApp.WriteLine($"Original target curves selected: {targetCurves.Count}");
            RhinoApp.WriteLine($"Placement curves used: {finalPlacementCurves.Count}");
            RhinoApp.WriteLine($"Pavers placed: {finalPavers.Count}");
            RhinoApp.WriteLine($"Gap: {gap.CurrentValue}");
            RhinoApp.WriteLine($"Expand pattern: {expandPattern.CurrentValue}");
            RhinoApp.WriteLine($"Pattern offset: {patternOffset.CurrentValue}");
            RhinoApp.WriteLine($"Pattern direction: {(flipPatternDirection.CurrentValue ? "Flipped" : "Original")}");
            RhinoApp.WriteLine($"Side: {sideOptions[sideIndex]}");
            RhinoApp.WriteLine($"Start: {startOptions[startIndex]}");
            RhinoApp.WriteLine($"Random start point: {(randomStartPoint.CurrentValue ? "Random" : "Original")}");
            RhinoApp.WriteLine($"Random scale: {(randomScale.CurrentValue ? "Yes" : "No")}");
            RhinoApp.WriteLine($"Random rotation: {(randomRotation.CurrentValue ? "Yes" : "No")}");

            return Result.Success;
        }

        private static List<Curve> CreatePlacementCurves(List<Curve> originalCurves, PavementSettings settings, double tolerance)
        {
            var result = new List<Curve>();

            foreach (var originalCurve in originalCurves)
            {
                result.Add(originalCurve.DuplicateCurve());

                if (settings.ExpandPatternCount <= 0)
                    continue;

                for (var i = 1; i <= settings.ExpandPatternCount; i++)
                {
                    var offsetDistance = settings.PatternOffset * i;

                    if (settings.FlipPatternDirection)
                        offsetDistance *= -1.0;

                    if (originalCurve.IsClosed)
                    {
                        if (!originalCurve.TryGetPlane(out var plane))
                            plane = Plane.WorldXY;

                        var orientation = originalCurve.ClosedCurveOrientation(plane);

                        if (orientation == CurveOrientation.Clockwise)
                            offsetDistance *= -1.0;

                        var offsetCurves = originalCurve.Offset(
                            plane,
                            offsetDistance,
                            tolerance,
                            CurveOffsetCornerStyle.Round
                        );

                        if (offsetCurves == null)
                            continue;

                        foreach (var offsetCurve in offsetCurves)
                        {
                            if (offsetCurve != null)
                                result.Add(offsetCurve);
                        }
                    }
                    else
                    {
                        if (!originalCurve.TryGetPlane(out var plane))
                            plane = Plane.WorldXY;

                        var offsetCurves = originalCurve.Offset(
                            plane,
                            offsetDistance,
                            tolerance,
                            CurveOffsetCornerStyle.Round
                        );

                        if (offsetCurves == null)
                            continue;

                        foreach (var offsetCurve in offsetCurves)
                        {
                            if (offsetCurve != null)
                                result.Add(offsetCurve);
                        }
                    }
                }
            }

            return result;
        }

        private static List<Curve> CreatePaversForTargets(Curve paverCurve, List<Curve> targetCurves, PavementSettings settings)
        {
            var result = new List<Curve>();

            for (var i = 0; i < targetCurves.Count; i++)
                result.AddRange(CreatePavers(paverCurve, targetCurves[i], settings, i));

            return result;
        }

        private static List<Curve> CreatePavers(Curve paverCurve, Curve targetCurve, PavementSettings settings, int targetIndex)
        {
            var result = new List<Curve>();

            if (!paverCurve.TryGetPlane(out var paverPlane))
                paverPlane = Plane.WorldXY;

            var paverBox = paverCurve.GetBoundingBox(paverPlane);

            var xSize = paverBox.Max.X - paverBox.Min.X;
            var ySize = paverBox.Max.Y - paverBox.Min.Y;

            var alongSize = Math.Max(xSize, ySize);
            var acrossSize = Math.Min(xSize, ySize);

            if (alongSize <= RhinoMath.ZeroTolerance)
                return result;

            var center = paverPlane.PointAt(
                (paverBox.Min.X + paverBox.Max.X) * 0.5,
                (paverBox.Min.Y + paverBox.Max.Y) * 0.5
            );

            var sourceXAxis = xSize >= ySize ? paverPlane.XAxis : paverPlane.YAxis;
            var sourceYAxis = xSize >= ySize ? paverPlane.YAxis : -paverPlane.XAxis;
            var sourcePlane = new Plane(center, sourceXAxis, sourceYAxis);

            var targetLength = targetCurve.GetLength();
            var baseSpacing = alongSize + settings.Gap;

            if (targetLength <= RhinoMath.ZeroTolerance || baseSpacing <= RhinoMath.ZeroTolerance)
                return result;

            var count = targetCurve.IsClosed
                ? Math.Max(1, (int)Math.Floor(targetLength / baseSpacing))
                : Math.Max(1, (int)Math.Floor((targetLength + settings.Gap) / baseSpacing));

            var actualSpacing = targetCurve.IsClosed
                ? targetLength / count
                : baseSpacing;

            var startDistance = 0.0;

            if (targetCurve.IsClosed && settings.RandomStartPoint)
            {
                var startRandom = new Random(1000 + targetIndex);
                startDistance = startRandom.NextDouble() * actualSpacing;
            }
            else if (!targetCurve.IsClosed && settings.StartIndex == 1)
            {
                startDistance = settings.Gap * 0.5;
            }

            var random = new Random(1 + targetIndex * 10000);

            for (var i = 0; i < count; i++)
            {
                var distance = startDistance + i * actualSpacing;

                if (!targetCurve.IsClosed && distance > targetLength)
                    break;

                if (targetCurve.IsClosed)
                    distance = distance % targetLength;

                if (!targetCurve.LengthParameter(distance, out var t))
                    continue;

                var point = targetCurve.PointAt(t);
                var tangent = targetCurve.TangentAt(t);

                if (!tangent.Unitize())
                    continue;

                Plane targetPlane;

                if (targetCurve.TryGetPlane(out var curvePlane))
                {
                    var zAxis = curvePlane.Normal;
                    var yAxis = Vector3d.CrossProduct(zAxis, tangent);

                    if (!yAxis.Unitize())
                        yAxis = sourcePlane.YAxis;

                    targetPlane = new Plane(point, tangent, yAxis);
                }
                else
                {
                    if (!targetCurve.PerpendicularFrameAt(t, out targetPlane))
                        targetPlane = new Plane(point, tangent, sourcePlane.YAxis);
                }

                var sideOffset = 0.0;

                if (settings.SideIndex == 1)
                    sideOffset = acrossSize * 0.5;

                if (settings.SideIndex == 2)
                    sideOffset = -acrossSize * 0.5;

                if (Math.Abs(sideOffset) > RhinoMath.ZeroTolerance)
                    targetPlane.Origin += targetPlane.YAxis * sideOffset;

                var copy = paverCurve.DuplicateCurve();

                var orient = Transform.PlaneToPlane(sourcePlane, targetPlane);
                copy.Transform(orient);

                if (settings.RandomRotation)
                {
                    var angle = RhinoMath.ToRadians((random.NextDouble() * 2.0 - 1.0) * settings.MaxRotationDegrees);
                    var rotate = Transform.Rotation(angle, targetPlane.ZAxis, targetPlane.Origin);
                    copy.Transform(rotate);
                }

                if (settings.RandomScale)
                {
                    var scaleFactor = 1.0 + ((random.NextDouble() * 2.0 - 1.0) * settings.MaxScalePercent / 100.0);
                    var scale = Transform.Scale(targetPlane.Origin, scaleFactor);
                    copy.Transform(scale);
                }

                result.Add(copy);
            }

            return result;
        }

        private static PaverSizes GetPaverSizes(Curve paverCurve)
        {
            if (!paverCurve.TryGetPlane(out var paverPlane))
                paverPlane = Plane.WorldXY;

            var paverBox = paverCurve.GetBoundingBox(paverPlane);

            var xSize = paverBox.Max.X - paverBox.Min.X;
            var ySize = paverBox.Max.Y - paverBox.Min.Y;

            return new PaverSizes
            {
                AlongSize = Math.Max(xSize, ySize),
                AcrossSize = Math.Min(xSize, ySize)
            };
        }

        private class PaverSizes
        {
            public double AlongSize { get; set; }
            public double AcrossSize { get; set; }
        }

        private class PavementSettings
        {
            public double Gap { get; set; }
            public int ExpandPatternCount { get; set; }
            public double PatternOffset { get; set; }
            public bool FlipPatternDirection { get; set; }
            public int SideIndex { get; set; }
            public int StartIndex { get; set; }
            public bool RandomStartPoint { get; set; }
            public bool RandomScale { get; set; }
            public double MaxScalePercent { get; set; }
            public bool RandomRotation { get; set; }
            public double MaxRotationDegrees { get; set; }
        }

        private class PavementPreviewConduit : DisplayConduit
        {
            public List<Curve> PreviewCurves { get; set; } = new List<Curve>();

            protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
            {
                foreach (var curve in PreviewCurves)
                    e.IncludeBoundingBox(curve.GetBoundingBox(true));
            }

            protected override void DrawOverlay(DrawEventArgs e)
            {
                var previewColor = System.Drawing.Color.FromArgb(246, 217, 245);

                foreach (var curve in PreviewCurves)
                    e.Display.DrawCurve(curve, previewColor, 2);
            }
        }
    }
}