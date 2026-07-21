using System;
using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Render;

namespace Numbat.Commands.Modelling.NumbatHandrail
{
    internal static class HandrailGenerator
    {
        private const int TopRailNone = 0;
        private const int TopRailRectangular = 1;
        private const int TopRailRound = 2;

        private const int BottomRailNone = 0;
        private const int BottomRailRaised = 2;

        private const int BayLayoutNone = 0;
        private const int BayLayoutAutomatic = 1;

        private const int InfillVertical = 0;
        private const int InfillZigZag = 1;
        private const int InfillPanel = 2;
        private const int InfillSheet = 3;
        private const int InfillEmpty = 4;

        private const int PanelFrameSolid = 0;
        private const int PanelFrameMitred = 1;

        public static HandrailGeometry CreateHandrailGeometry(Curve originalCurve, HandrailSettings settings, double tolerance)
        {
            return CreateHandrailGeometry(originalCurve, settings, tolerance, true, true, true, true, true);
        }

        public static HandrailGeometry CreateHandrailGeometry(IReadOnlyList<Curve> runs, HandrailSettings settings, double tolerance)
        {
            var combined = new HandrailGeometry();

            if (runs == null || runs.Count == 0)
                return combined;

            for (var i = 0; i < runs.Count; i++)
            {
                var run = runs[i];

                if (run == null || run.GetLength() <= RhinoMath.ZeroTolerance)
                    continue;

                var runGeometry = CreateHandrailGeometry(
                    run,
                    settings,
                    tolerance,
                    includeStartPost: i == 0,
                    includeEndPost: true,
                    allowStartTab: i == 0,
                    allowEndTab: i == runs.Count - 1,
                    includeHeightDimension: i == 0
                );

                combined.Append(runGeometry);
            }

            return combined;
        }

        private static HandrailGeometry CreateHandrailGeometry(
            Curve originalCurve,
            HandrailSettings settings,
            double tolerance,
            bool includeStartPost,
            bool includeEndPost,
            bool allowStartTab,
            bool allowEndTab,
            bool includeHeightDimension)
        {
            var geometry = new HandrailGeometry();
            var workingCurve = originalCurve.DuplicateCurve();
            Curve originalStartCurve = null;
            Curve originalEndCurve = null;

            if (settings.Tabs && (allowStartTab || allowEndTab))
            {
                var curveLength = workingCurve.GetLength();
                var requiredLength = settings.TabLength * ((allowStartTab ? 1.0 : 0.0) + (allowEndTab ? 1.0 : 0.0));

                if (curveLength <= requiredLength)
                    return geometry;

                var trimStart = workingCurve.Domain.Min;
                var trimEnd = workingCurve.Domain.Max;

                if (allowStartTab)
                {
                    if (!workingCurve.LengthParameter(settings.TabLength, out trimStart))
                        return geometry;

                    originalStartCurve = workingCurve.Trim(workingCurve.Domain.Min, trimStart);
                }

                if (allowEndTab)
                {
                    if (!workingCurve.LengthParameter(curveLength - settings.TabLength, out trimEnd))
                        return geometry;

                    originalEndCurve = workingCurve.Trim(trimEnd, workingCurve.Domain.Max);
                }

                workingCurve = workingCurve.Trim(trimStart, trimEnd);

                if (workingCurve == null)
                    return geometry;
            }

            var railLength = workingCurve.GetLength();

            if (railLength <= RhinoMath.ZeroTolerance)
                return geometry;

            var bottomRailBottomZ = GetBottomRailBottomZ(settings);
            var infillBottomZ = GetInfillBottomZ(settings, bottomRailBottomZ);
            var infillTopZ = GetInfillTopZ(settings);
            var postBottomZ = GetPostBottomZ(settings, bottomRailBottomZ);
            var postTopZ = settings.GroundZ + settings.Height;

            CreateTopRail(geometry, workingCurve, settings, tolerance);
            CreateBottomRail(geometry, workingCurve, settings, bottomRailBottomZ, tolerance);

            var postDistances = CreatePostDistances(workingCurve, settings);

            if (postDistances.Count < 2)
            {
                postDistances.Clear();
                postDistances.Add(0.0);
                postDistances.Add(railLength);
            }

            var startPost = CreateVerticalElementAtDistance(
                workingCurve,
                0.0,
                settings.BoxRailHeight,
                settings.BoxRailDepth,
                postBottomZ,
                postTopZ
            );

            if (includeStartPost && startPost != null)
                geometry.EndPosts.Add(startPost);

            var endPost = CreateVerticalElementAtDistance(
                workingCurve,
                railLength,
                settings.BoxRailHeight,
                settings.BoxRailDepth,
                postBottomZ,
                postTopZ
            );

            if (includeEndPost && endPost != null)
                geometry.EndPosts.Add(endPost);

            for (var i = 1; i < postDistances.Count - 1; i++)
            {
                var post = CreateVerticalElementAtDistance(
                    workingCurve,
                    postDistances[i],
                    settings.BoxRailHeight,
                    settings.BoxRailDepth,
                    postBottomZ,
                    postTopZ
                );

                if (post != null)
                    geometry.IntermediatePosts.Add(post);
            }

            if (settings.BottomRailModeIndex == BottomRailRaised && settings.SupportFeet)
            {
                var supportFootDistances = new List<double>(postDistances);

                if (!includeStartPost && supportFootDistances.Count > 0)
                    supportFootDistances.RemoveAt(0);

                if (!includeEndPost && supportFootDistances.Count > 0)
                    supportFootDistances.RemoveAt(supportFootDistances.Count - 1);

                geometry.SupportFeet.AddRange(CreateSupportFeetAtPostDistances(
                    workingCurve,
                    supportFootDistances,
                    settings.BoxRailHeight,
                    settings.BoxRailDepth,
                    settings.GroundZ,
                    bottomRailBottomZ
                ));
            }

            if (settings.PreviewDims)
                AddPreviewLabels(geometry, workingCurve, postDistances, settings, includeHeightDimension);

            for (var i = 0; i < postDistances.Count - 1; i++)
            {
                var bay = CreateBayCurve(workingCurve, postDistances[i], postDistances[i + 1]);

                if (bay == null || bay.GetLength() <= RhinoMath.ZeroTolerance)
                    continue;

                if (settings.InfillStyleIndex == InfillVertical)
                {
                    geometry.Infill.AddRange(CreateVerticalInfill(
                        bay,
                        settings.InfillWidth,
                        settings.InfillDepth,
                        infillBottomZ,
                        infillTopZ,
                        settings.MaxInfillSpacing
                    ));
                }
                else if (settings.InfillStyleIndex == InfillZigZag)
                {
                    geometry.Infill.AddRange(CreateHardZigZagInfill(
                        bay,
                        infillBottomZ,
                        infillTopZ,
                        settings.ZigZagDiameter,
                        settings.ZigZagBayLength,
                        tolerance
                    ));
                }
                else if (settings.InfillStyleIndex == InfillPanel)
                {
                    CreatePanelInfill(
                        geometry,
                        bay,
                        settings,
                        tolerance
                    );
                }
                else if (settings.InfillStyleIndex == InfillSheet)
                {
                    CreateSheetInfill(
                        geometry,
                        bay,
                        settings,
                        infillBottomZ,
                        infillTopZ,
                        tolerance
                    );
                }
                else if (settings.InfillStyleIndex == InfillEmpty)
                {
                    // Intentionally leave the bay open.
                }
            }

            if (settings.Tabs && (allowStartTab || allowEndTab))
            {
                var upperTabZ = settings.GroundZ + settings.Height - 75.0;
                var lowerTabZ = bottomRailBottomZ + 75.0;

                if (settings.BottomRailModeIndex == BottomRailNone)
                    lowerTabZ = settings.GroundZ + 75.0;

                if (originalStartCurve != null)
                {
                    geometry.Tabs.AddRange(CreateSweptRectangularRail(MoveCurveToZ(originalStartCurve, upperTabZ), settings.BoxRailDepth, settings.BoxRailHeight, tolerance));
                    geometry.Tabs.AddRange(CreateSweptRectangularRail(MoveCurveToZ(originalStartCurve, lowerTabZ), settings.BoxRailDepth, settings.BoxRailHeight, tolerance));
                }

                if (originalEndCurve != null)
                {
                    geometry.Tabs.AddRange(CreateSweptRectangularRail(MoveCurveToZ(originalEndCurve, upperTabZ), settings.BoxRailDepth, settings.BoxRailHeight, tolerance));
                    geometry.Tabs.AddRange(CreateSweptRectangularRail(MoveCurveToZ(originalEndCurve, lowerTabZ), settings.BoxRailDepth, settings.BoxRailHeight, tolerance));
                }
            }

            return geometry;
        }

        public static bool IsCurveFlatInZ(Curve curve, double tolerance, out double groundZ)
        {
            var bbox = curve.GetBoundingBox(true);
            groundZ = bbox.Min.Z;

            return Math.Abs(bbox.Max.Z - bbox.Min.Z) <= tolerance;
        }

        public static void AddGeometryToDocument(RhinoDoc doc, HandrailGeometry geometry, HandrailSettings settings)
        {
            var allBreps = geometry.AllBreps();

            if (allBreps.Count == 0)
                return;

            var parentLayerIndex = EnsureLayer(doc, "nbHandrail", -1, System.Drawing.Color.FromArgb(214, 218, 216));
            var definitionGeometry = new List<GeometryBase>();
            var definitionAttributes = new List<ObjectAttributes>();

            AddBrepsToBlockIfAny(doc, geometry.TopRails, "Top Rails", parentLayerIndex, System.Drawing.Color.FromArgb(191, 214, 180), definitionGeometry, definitionAttributes);
            AddBrepsToBlockIfAny(doc, geometry.BottomRails, "Bottom Rails", parentLayerIndex, System.Drawing.Color.FromArgb(178, 204, 218), definitionGeometry, definitionAttributes);
            AddBrepsToBlockIfAny(doc, geometry.Infill, "Infill", parentLayerIndex, System.Drawing.Color.FromArgb(224, 204, 170), definitionGeometry, definitionAttributes);
            AddBrepsToBlockIfAny(doc, geometry.PanelFrames, "Panel Frames", parentLayerIndex, System.Drawing.Color.FromArgb(205, 190, 220), definitionGeometry, definitionAttributes);
            AddBrepsToBlockIfAny(doc, geometry.PanelSheets, "Panel Sheets", parentLayerIndex, System.Drawing.Color.FromArgb(184, 215, 211), definitionGeometry, definitionAttributes);
            AddBrepsToBlockIfAny(doc, geometry.EndPosts, "End Posts", parentLayerIndex, System.Drawing.Color.FromArgb(218, 186, 176), definitionGeometry, definitionAttributes);
            AddBrepsToBlockIfAny(doc, geometry.IntermediatePosts, "Intermediate Posts", parentLayerIndex, System.Drawing.Color.FromArgb(218, 204, 158), definitionGeometry, definitionAttributes);
            AddBrepsToBlockIfAny(doc, geometry.SupportFeet, "Support Feet", parentLayerIndex, System.Drawing.Color.FromArgb(190, 197, 218), definitionGeometry, definitionAttributes);
            AddBrepsToBlockIfAny(doc, geometry.Tabs, "Tabs", parentLayerIndex, System.Drawing.Color.FromArgb(224, 188, 206), definitionGeometry, definitionAttributes);

            if (definitionGeometry.Count == 0)
                return;

            var definitionName = CreateUniqueBlockName(doc, "nbHandrail");
            var description = CreateHandrailDescription(settings);
            var definitionIndex = doc.InstanceDefinitions.Add(
                definitionName,
                description,
                Point3d.Origin,
                definitionGeometry,
                definitionAttributes
            );

            if (definitionIndex < 0)
                return;

            var instanceAttributes = new ObjectAttributes
            {
                LayerIndex = parentLayerIndex,
                Name = definitionName
            };

            var instanceId = doc.Objects.AddInstanceObject(definitionIndex, Transform.Identity, instanceAttributes);

            if (instanceId == Guid.Empty)
            {
                var failedDefinition = doc.InstanceDefinitions[definitionIndex];

                if (failedDefinition != null)
                    doc.InstanceDefinitions.Delete(failedDefinition);
                return;
            }

            var definition = doc.InstanceDefinitions[definitionIndex];

            if (definition != null)
            {
                foreach (var definitionObject in definition.GetObjects())
                {
                    if (definitionObject != null)
                        ApplyTwoMeterBoxMapping(definitionObject);
                }
            }
        }

        private static void AddBrepsToBlockIfAny(
            RhinoDoc doc,
            List<Brep> breps,
            string layerName,
            int parentLayerIndex,
            System.Drawing.Color layerColor,
            List<GeometryBase> definitionGeometry,
            List<ObjectAttributes> definitionAttributes)
        {
            if (breps.Count == 0)
                return;

            var layerIndex = EnsureLayer(doc, layerName, parentLayerIndex, layerColor);

            foreach (var brep in breps)
            {
                if (brep == null)
                    continue;

                definitionGeometry.Add(brep.DuplicateBrep());
                definitionAttributes.Add(new ObjectAttributes { LayerIndex = layerIndex });
            }
        }

        private static string CreateUniqueBlockName(RhinoDoc doc, string baseName)
        {
            var suffix = 1;
            var candidate = baseName;

            while (doc.InstanceDefinitions.Find(candidate) != null)
            {
                suffix++;
                candidate = baseName + " " + suffix;
            }

            return candidate;
        }

        private static string CreateHandrailDescription(HandrailSettings settings)
        {
            if (settings == null)
                return "Generated by nbHandrail";

            return string.Join(System.Environment.NewLine, new[]
            {
                "Generated by nbHandrail",
                "Height=" + settings.Height,
                "TopRailStyleIndex=" + settings.TopRailStyleIndex,
                "BoxRailDepth=" + settings.BoxRailDepth,
                "BoxRailHeight=" + settings.BoxRailHeight,
                "TopRailDiameter=" + settings.TopRailDiameter,
                "BottomRailModeIndex=" + settings.BottomRailModeIndex,
                "BottomRailHeight=" + settings.BottomRailHeight,
                "SupportFeet=" + settings.SupportFeet,
                "BayLayoutIndex=" + settings.BayLayoutIndex,
                "MaxBayLength=" + settings.MaxBayLength,
                "Tabs=" + settings.Tabs,
                "TabLength=" + settings.TabLength,
                "InfillStyleIndex=" + settings.InfillStyleIndex,
                "InfillWidth=" + settings.InfillWidth,
                "InfillDepth=" + settings.InfillDepth,
                "MaxInfillSpacing=" + settings.MaxInfillSpacing,
                "ZigZagDiameter=" + settings.ZigZagDiameter,
                "ZigZagBayLength=" + settings.ZigZagBayLength,
                "PanelGap=" + settings.PanelGap,
                "PanelFrameWidth=" + settings.PanelFrameWidth,
                "PanelFrameDepth=" + settings.PanelFrameDepth,
                "PanelSheetThickness=" + settings.PanelSheetThickness,
                "PanelTopGap=" + settings.PanelTopGap,
                "PanelBottomGap=" + settings.PanelBottomGap,
                "PanelFrameConstructionIndex=" + settings.PanelFrameConstructionIndex
            });
        }

        private static double GetBottomRailBottomZ(HandrailSettings settings)
        {
            if (settings.BottomRailModeIndex == BottomRailRaised)
                return settings.GroundZ + settings.BottomRailHeight;

            return settings.GroundZ;
        }

        private static double GetInfillBottomZ(HandrailSettings settings, double bottomRailBottomZ)
        {
            if (settings.BottomRailModeIndex == BottomRailNone)
                return settings.GroundZ;

            return bottomRailBottomZ + settings.BoxRailHeight;
        }

        private static double GetPostBottomZ(HandrailSettings settings, double bottomRailBottomZ)
        {
            if (settings.BottomRailModeIndex == BottomRailRaised)
                return bottomRailBottomZ;

            return settings.GroundZ;
        }

        private static double GetInfillTopZ(HandrailSettings settings)
        {
            if (settings.TopRailStyleIndex == TopRailNone)
                return settings.GroundZ + settings.Height;

            if (settings.TopRailStyleIndex == TopRailRound)
                return settings.GroundZ + settings.Height - settings.TopRailDiameter;

            return settings.GroundZ + settings.Height - settings.BoxRailHeight;
        }

        private static void CreateTopRail(HandrailGeometry geometry, Curve workingCurve, HandrailSettings settings, double tolerance)
        {
            if (settings.TopRailStyleIndex == TopRailNone)
                return;

            if (settings.TopRailStyleIndex == TopRailRound)
            {
                var centerZ = settings.GroundZ + settings.Height - settings.TopRailDiameter * 0.5;
                var topRailCurve = MoveCurveToZ(workingCurve, centerZ);
                geometry.TopRails.AddRange(CreatePipe(topRailCurve, settings.TopRailDiameter, tolerance));
            }
            else
            {
                var centerZ = settings.GroundZ + settings.Height - settings.BoxRailHeight * 0.5;
                var topRailCurve = MoveCurveToZ(workingCurve, centerZ);
                geometry.TopRails.AddRange(CreateSweptRectangularRail(topRailCurve, settings.BoxRailDepth, settings.BoxRailHeight, tolerance));
            }
        }

        private static void CreateBottomRail(HandrailGeometry geometry, Curve workingCurve, HandrailSettings settings, double bottomRailBottomZ, double tolerance)
        {
            if (settings.BottomRailModeIndex == BottomRailNone)
                return;

            var bottomRailCenterZ = bottomRailBottomZ + settings.BoxRailHeight * 0.5;
            var bottomRailCurve = MoveCurveToZ(workingCurve, bottomRailCenterZ);
            geometry.BottomRails.AddRange(CreateSweptRectangularRail(bottomRailCurve, settings.BoxRailDepth, settings.BoxRailHeight, tolerance));
        }

        private static List<double> CreatePostDistances(Curve curve, HandrailSettings settings)
        {
            var distances = new List<double>();
            var length = curve.GetLength();

            distances.Add(0.0);

            if (settings.BayLayoutIndex == BayLayoutAutomatic)
            {
                var maxBayLength = settings.MaxBayLength;

                if (length > RhinoMath.ZeroTolerance && maxBayLength > RhinoMath.ZeroTolerance)
                {
                    var bayCount = Math.Max(1, (int)Math.Ceiling(length / maxBayLength));
                    var actualBayLength = length / bayCount;

                    for (var i = 1; i < bayCount; i++)
                        distances.Add(i * actualBayLength);
                }
            }

            if (distances[distances.Count - 1] < length)
                distances.Add(length);

            return distances;
        }

        private static Curve CreateBayCurve(Curve curve, double startDistance, double endDistance)
        {
            if (!curve.LengthParameter(startDistance, out var startT))
                return null;

            if (!curve.LengthParameter(endDistance, out var endT))
                return null;

            return curve.Trim(startT, endT);
        }

        private static Curve MoveCurveToZ(Curve curve, double targetZ)
        {
            var copy = curve.DuplicateCurve();
            var bbox = copy.GetBoundingBox(true);
            var currentZ = bbox.Min.Z;
            var xform = Transform.Translation(0.0, 0.0, targetZ - currentZ);
            copy.Transform(xform);
            return copy;
        }

        private static List<Brep> CreateSweptRectangularRail(Curve path, double depth, double height, double tolerance)
        {
            var breps = new List<Brep>();

            var tangent = path.TangentAtStart;
            tangent.Z = 0.0;

            if (!tangent.Unitize())
                return breps;

            var zAxis = Vector3d.ZAxis;
            var outward = Vector3d.CrossProduct(zAxis, tangent);

            if (!outward.Unitize())
                return breps;

            var profilePlane = new Plane(path.PointAtStart, outward, zAxis);

            var profile = new Rectangle3d(
                profilePlane,
                new Interval(-depth * 0.5, depth * 0.5),
                new Interval(-height * 0.5, height * 0.5)
            ).ToNurbsCurve();

            var sweep = new SweepOneRail
            {
                SweepTolerance = tolerance,
                AngleToleranceRadians = RhinoMath.ToRadians(1.0),
                ClosedSweep = false
            };

            var sweptBreps = sweep.PerformSweep(path, profile);

            if (sweptBreps == null)
                return breps;

            foreach (var brep in sweptBreps)
            {
                if (brep == null)
                    continue;

                var capped = brep.CapPlanarHoles(tolerance);
                breps.Add(capped ?? brep);
            }

            return breps;
        }

        private static List<Brep> CreatePipe(Curve rail, double diameter, double tolerance)
        {
            var breps = new List<Brep>();

            if (diameter <= RhinoMath.ZeroTolerance)
                return breps;

            var pipes = Brep.CreatePipe(
                rail,
                diameter * 0.5,
                false,
                PipeCapMode.Flat,
                true,
                tolerance,
                RhinoMath.ToRadians(1.0)
            );

            if (pipes == null)
                return breps;

            foreach (var pipe in pipes)
            {
                if (pipe != null)
                    breps.Add(pipe);
            }

            return breps;
        }

        private static List<Brep> CreateVerticalInfill(Curve bayCurve, double widthAlongCurve, double depthPerpendicular, double bottomZ, double topZ, double maxSpacing)
        {
            var breps = new List<Brep>();
            var length = bayCurve.GetLength();

            if (length <= RhinoMath.ZeroTolerance || maxSpacing <= RhinoMath.ZeroTolerance)
                return breps;

            var divisionCount = Math.Max(1, (int)Math.Ceiling(length / maxSpacing));
            var spacing = length / divisionCount;

            for (var i = 1; i < divisionCount; i++)
            {
                var distance = i * spacing;
                var brep = CreateVerticalElementAtDistance(bayCurve, distance, widthAlongCurve, depthPerpendicular, bottomZ, topZ);

                if (brep != null)
                    breps.Add(brep);
            }

            return breps;
        }

        private static List<Brep> CreateHardZigZagInfill(Curve bayCurve, double bottomZ, double topZ, double diameter, double bayLength, double tolerance)
        {
            var breps = new List<Brep>();
            var length = bayCurve.GetLength();

            if (length <= RhinoMath.ZeroTolerance || bayLength <= RhinoMath.ZeroTolerance || diameter <= RhinoMath.ZeroTolerance)
                return breps;

            var stationCount = Math.Max(1, (int)Math.Ceiling(length / bayLength));
            var actualBayLength = length / stationCount;

            var previousPoint = PointAtDistanceAndZ(bayCurve, 0.0, topZ);

            for (var i = 1; i <= stationCount; i++)
            {
                var distance = i * actualBayLength;

                if (distance > length)
                    distance = length;

                var nextZ = i % 2 == 1 ? bottomZ : topZ;
                var nextPoint = PointAtDistanceAndZ(bayCurve, distance, nextZ);
                var line = new Line(previousPoint, nextPoint);

                if (line.Length > RhinoMath.ZeroTolerance)
                    breps.AddRange(CreatePipe(new LineCurve(line), diameter, tolerance));

                previousPoint = nextPoint;
            }

            return breps;
        }


        private static void CreatePanelInfill(HandrailGeometry geometry, Curve bayCurve, HandrailSettings settings, double tolerance)
        {
            var length = bayCurve.GetLength();
            var postWidthAlongCurve = settings.BoxRailHeight;
            var requestedGap = Math.Max(0.0, settings.PanelGap);
            var frameWidth = Math.Max(0.0, settings.PanelFrameWidth);
            var frameDepth = Math.Max(0.0, settings.PanelFrameDepth);
            var sheetThickness = Math.Max(0.0, settings.PanelSheetThickness);
            var panelBottomZ = settings.GroundZ + Math.Max(0.0, settings.PanelBottomGap);
            var panelTopZ = settings.GroundZ + settings.Height - Math.Max(0.0, settings.PanelTopGap);

            if (length <= RhinoMath.ZeroTolerance || frameWidth <= RhinoMath.ZeroTolerance || frameDepth <= RhinoMath.ZeroTolerance || sheetThickness <= RhinoMath.ZeroTolerance)
                return;

            if (panelTopZ - panelBottomZ <= frameWidth * 2.0)
            {
                geometry.PanelBaysOmitted++;
                return;
            }

            var minimumPanelWidth = frameWidth * 2.0 + Math.Max(sheetThickness, 1.0);
            var availableBetweenPostFaces = length - postWidthAlongCurve;
            var effectiveGap = requestedGap;
            var panelOuterWidth = availableBetweenPostFaces - effectiveGap * 2.0;

            if (panelOuterWidth < minimumPanelWidth)
            {
                effectiveGap = Math.Max(0.0, (availableBetweenPostFaces - minimumPanelWidth) * 0.5);
                panelOuterWidth = availableBetweenPostFaces - effectiveGap * 2.0;
                geometry.PanelBaysReduced++;
            }

            if (panelOuterWidth < minimumPanelWidth || panelOuterWidth <= RhinoMath.ZeroTolerance)
            {
                geometry.PanelBaysOmitted++;
                return;
            }

            var panelStartDistance = postWidthAlongCurve * 0.5 + effectiveGap;
            var panelEndDistance = panelStartDistance + panelOuterWidth;

            if (!TryCreatePanelPlane(bayCurve, panelStartDistance, panelEndDistance, panelBottomZ, out var panelPlane, out var panelWidth))
            {
                geometry.PanelBaysOmitted++;
                return;
            }

            if (settings.PanelFrameConstructionIndex == PanelFrameMitred)
                geometry.PanelFrames.AddRange(CreateMitredPanelFrame(panelPlane, panelWidth, panelTopZ - panelBottomZ, frameWidth, frameDepth, tolerance));
            else
                AddIfNotNull(geometry.PanelFrames, CreateSolidPanelFrame(panelPlane, panelWidth, panelTopZ - panelBottomZ, frameWidth, frameDepth, tolerance));

            AddIfNotNull(geometry.PanelSheets, CreatePanelSheet(panelPlane, panelWidth, panelTopZ - panelBottomZ, frameWidth, sheetThickness));

            CreatePanelTabs(
                geometry,
                bayCurve,
                postWidthAlongCurve,
                panelStartDistance,
                panelEndDistance,
                panelBottomZ,
                panelTopZ,
                frameWidth,
                frameDepth,
                tolerance
            );
        }

        private static void CreateSheetInfill(HandrailGeometry geometry, Curve bayCurve, HandrailSettings settings, double sheetBottomZ, double sheetTopZ, double tolerance)
        {
            var length = bayCurve.GetLength();
            var postWidthAlongCurve = settings.BoxRailHeight;
            var sheetHeight = sheetTopZ - sheetBottomZ;
            var sheetWidth = length - postWidthAlongCurve;

            if (length <= RhinoMath.ZeroTolerance || sheetWidth <= RhinoMath.ZeroTolerance || sheetHeight <= RhinoMath.ZeroTolerance)
            {
                geometry.SheetBaysOmitted++;
                return;
            }

            var sheetStartDistance = postWidthAlongCurve * 0.5;
            var sheetEndDistance = length - postWidthAlongCurve * 0.5;

            if (!TryCreatePanelPlane(bayCurve, sheetStartDistance, sheetEndDistance, sheetBottomZ, out var sheetPlane, out var actualSheetWidth))
            {
                geometry.SheetBaysOmitted++;
                return;
            }

            AddIfNotNull(geometry.PanelSheets, CreatePlanarSheet(sheetPlane, actualSheetWidth, sheetHeight, tolerance));
        }

        private static void CreatePanelTabs(
            HandrailGeometry geometry,
            Curve bayCurve,
            double postWidthAlongCurve,
            double panelStartDistance,
            double panelEndDistance,
            double panelBottomZ,
            double panelTopZ,
            double tabWidth,
            double tabDepth,
            double tolerance
        )
        {
            var length = bayCurve.GetLength();
            var leftStartDistance = postWidthAlongCurve * 0.5;
            var leftEndDistance = panelStartDistance;
            var rightStartDistance = panelEndDistance;
            var rightEndDistance = length - postWidthAlongCurve * 0.5;

            var panelHeight = panelTopZ - panelBottomZ;

            if (panelHeight <= RhinoMath.ZeroTolerance)
                return;

            var lowerTabZ = panelBottomZ + 75.0;
            var upperTabZ = panelTopZ - 75.0;

            if (upperTabZ <= lowerTabZ)
            {
                lowerTabZ = panelBottomZ + panelHeight * 0.25;
                upperTabZ = panelBottomZ + panelHeight * 0.75;
            }

            AddPanelTabIfPossible(geometry, bayCurve, leftStartDistance, leftEndDistance, lowerTabZ, tabWidth, tabDepth, tolerance);
            AddPanelTabIfPossible(geometry, bayCurve, leftStartDistance, leftEndDistance, upperTabZ, tabWidth, tabDepth, tolerance);
            AddPanelTabIfPossible(geometry, bayCurve, rightStartDistance, rightEndDistance, lowerTabZ, tabWidth, tabDepth, tolerance);
            AddPanelTabIfPossible(geometry, bayCurve, rightStartDistance, rightEndDistance, upperTabZ, tabWidth, tabDepth, tolerance);
        }

        private static void AddPanelTabIfPossible(
            HandrailGeometry geometry,
            Curve bayCurve,
            double startDistance,
            double endDistance,
            double z,
            double tabWidth,
            double tabDepth,
            double tolerance
        )
        {
            if (endDistance - startDistance <= tolerance)
                return;

            var tabCurve = CreateBayCurve(bayCurve, startDistance, endDistance);

            if (tabCurve == null || tabCurve.GetLength() <= tolerance)
                return;

            geometry.Tabs.AddRange(CreateSweptRectangularRail(
                MoveCurveToZ(tabCurve, z),
                tabDepth,
                tabWidth,
                tolerance
            ));
        }

        private static bool TryCreatePanelPlane(Curve bayCurve, double startDistance, double endDistance, double bottomZ, out Plane panelPlane, out double panelWidth)
        {
            panelPlane = Plane.Unset;
            panelWidth = 0.0;

            var start = PointAtDistanceAndZ(bayCurve, startDistance, bottomZ);
            var end = PointAtDistanceAndZ(bayCurve, endDistance, bottomZ);
            var xAxis = end - start;

            if (!xAxis.Unitize())
                return false;

            var yAxis = Vector3d.CrossProduct(Vector3d.ZAxis, xAxis);

            if (!yAxis.Unitize())
                return false;

            panelWidth = start.DistanceTo(end);
            panelPlane = new Plane(start, xAxis, yAxis);
            return panelWidth > RhinoMath.ZeroTolerance;
        }

        private static Brep CreatePlanarSheet(Plane sheetPlane, double width, double height, double tolerance)
        {
            if (width <= RhinoMath.ZeroTolerance || height <= RhinoMath.ZeroTolerance)
                return null;

            var outline = CreateRectangleCurve(sheetPlane, 0.0, width, 0.0, height);
            var planar = Brep.CreatePlanarBreps(outline, tolerance);

            if (planar == null || planar.Length == 0)
                return null;

            return planar[0];
        }

        private static Brep CreatePanelSheet(Plane panelPlane, double outerWidth, double outerHeight, double frameWidth, double sheetThickness)
        {
            var sheetWidth = outerWidth - frameWidth * 2.0;
            var sheetHeight = outerHeight - frameWidth * 2.0;

            if (sheetWidth <= RhinoMath.ZeroTolerance || sheetHeight <= RhinoMath.ZeroTolerance)
                return null;

            var box = new Box(
                panelPlane,
                new Interval(frameWidth, frameWidth + sheetWidth),
                new Interval(-sheetThickness * 0.5, sheetThickness * 0.5),
                new Interval(frameWidth, frameWidth + sheetHeight)
            );

            return box.ToBrep();
        }

        private static Brep CreateSolidPanelFrame(Plane panelPlane, double outerWidth, double outerHeight, double frameWidth, double frameDepth, double tolerance)
        {
            if (outerWidth <= frameWidth * 2.0 || outerHeight <= frameWidth * 2.0)
                return null;

            var outer = CreateRectangleCurve(panelPlane, 0.0, outerWidth, 0.0, outerHeight);
            var inner = CreateRectangleCurve(panelPlane, frameWidth, outerWidth - frameWidth, frameWidth, outerHeight - frameWidth);
            var planar = Brep.CreatePlanarBreps(new Curve[] { outer, inner }, tolerance);

            if (planar == null || planar.Length == 0 || planar[0] == null || planar[0].Faces.Count == 0)
                return null;

            var path = new LineCurve(
                panelPlane.Origin - panelPlane.YAxis * (frameDepth * 0.5),
                panelPlane.Origin + panelPlane.YAxis * (frameDepth * 0.5)
            );

            var extrusion = planar[0].Faces[0].CreateExtrusion(path, true);
            return extrusion;
        }

        private static List<Brep> CreateMitredPanelFrame(Plane panelPlane, double outerWidth, double outerHeight, double frameWidth, double frameDepth, double tolerance)
        {
            var breps = new List<Brep>();

            if (outerWidth <= frameWidth * 2.0 || outerHeight <= frameWidth * 2.0)
                return breps;

            AddIfNotNull(breps, CreateExtrudedPanelMember(panelPlane, new[]
            {
                new Point2d(0.0, 0.0),
                new Point2d(outerWidth, 0.0),
                new Point2d(outerWidth - frameWidth, frameWidth),
                new Point2d(frameWidth, frameWidth)
            }, frameDepth, tolerance));

            AddIfNotNull(breps, CreateExtrudedPanelMember(panelPlane, new[]
            {
                new Point2d(outerWidth, 0.0),
                new Point2d(outerWidth, outerHeight),
                new Point2d(outerWidth - frameWidth, outerHeight - frameWidth),
                new Point2d(outerWidth - frameWidth, frameWidth)
            }, frameDepth, tolerance));

            AddIfNotNull(breps, CreateExtrudedPanelMember(panelPlane, new[]
            {
                new Point2d(outerWidth, outerHeight),
                new Point2d(0.0, outerHeight),
                new Point2d(frameWidth, outerHeight - frameWidth),
                new Point2d(outerWidth - frameWidth, outerHeight - frameWidth)
            }, frameDepth, tolerance));

            AddIfNotNull(breps, CreateExtrudedPanelMember(panelPlane, new[]
            {
                new Point2d(0.0, outerHeight),
                new Point2d(0.0, 0.0),
                new Point2d(frameWidth, frameWidth),
                new Point2d(frameWidth, outerHeight - frameWidth)
            }, frameDepth, tolerance));

            return breps;
        }

        private static Brep CreateExtrudedPanelMember(Plane panelPlane, Point2d[] points, double depth, double tolerance)
        {
            var polyline = new Polyline();

            foreach (var point in points)
                polyline.Add(panelPlane.PointAt(point.X, 0.0, point.Y));

            polyline.Add(panelPlane.PointAt(points[0].X, 0.0, points[0].Y));

            var curve = polyline.ToNurbsCurve();
            var planar = Brep.CreatePlanarBreps(curve, tolerance);

            if (planar == null || planar.Length == 0 || planar[0] == null || planar[0].Faces.Count == 0)
                return null;

            var path = new LineCurve(
                panelPlane.Origin - panelPlane.YAxis * (depth * 0.5),
                panelPlane.Origin + panelPlane.YAxis * (depth * 0.5)
            );

            return planar[0].Faces[0].CreateExtrusion(path, true);
        }

        private static Curve CreateRectangleCurve(Plane plane, double minX, double maxX, double minZ, double maxZ)
        {
            var polyline = new Polyline
            {
                plane.PointAt(minX, 0.0, minZ),
                plane.PointAt(maxX, 0.0, minZ),
                plane.PointAt(maxX, 0.0, maxZ),
                plane.PointAt(minX, 0.0, maxZ),
                plane.PointAt(minX, 0.0, minZ)
            };

            return polyline.ToNurbsCurve();
        }

        private static void AddIfNotNull(List<Brep> breps, Brep brep)
        {
            if (brep != null)
                breps.Add(brep);
        }

        private static void AddPreviewLabels(HandrailGeometry geometry, Curve path, List<double> postDistances, HandrailSettings settings, bool includeHeightDimension)
        {
            var railLength = path.GetLength();
            var labelZ = settings.GroundZ + settings.Height + 100.0;
            var bayLineZ = settings.GroundZ + settings.Height + 60.0;
            var totalLineZ = settings.GroundZ + settings.Height + 180.0;
            var tickHalfLength = 45.0;
            var heightDimOffset = 250.0;

            for (var i = 0; i < postDistances.Count - 1; i++)
            {
                var startDistance = postDistances[i];
                var endDistance = postDistances[i + 1];
                var midDistance = (startDistance + endDistance) * 0.5;

                var labelPoint = PointAtDistanceAndZ(path, midDistance, labelZ);
                geometry.PreviewLabels.Add(new HandrailPreviewLabel(labelPoint, FormatMillimetres(endDistance - startDistance)));

                if (TryGetOutwardAtDistance(path, midDistance, out var bayTickDirection))
                {
                    var lineStart = PointAtDistanceAndZ(path, startDistance, bayLineZ);
                    var lineEnd = PointAtDistanceAndZ(path, endDistance, bayLineZ);

                    AddPreviewLine(geometry, lineStart, lineEnd);
                    AddCenteredTick(geometry, lineStart, bayTickDirection, tickHalfLength);
                    AddCenteredTick(geometry, lineEnd, bayTickDirection, tickHalfLength);
                }
            }

            geometry.PreviewLabels.Add(new HandrailPreviewLabel(PointAtDistanceAndZ(path, railLength * 0.5, labelZ + 120.0), "Total length: " + FormatMillimetres(railLength)));

            if (TryGetOutwardAtDistance(path, railLength * 0.5, out var totalTickDirection))
            {
                var totalLineStart = PointAtDistanceAndZ(path, 0.0, totalLineZ);
                var totalLineEnd = PointAtDistanceAndZ(path, railLength, totalLineZ);

                AddPreviewLine(geometry, totalLineStart, totalLineEnd);
                AddCenteredTick(geometry, totalLineStart, totalTickDirection, tickHalfLength);
                AddCenteredTick(geometry, totalLineEnd, totalTickDirection, tickHalfLength);
            }

            if (includeHeightDimension)
            {
                var heightPoint = PointAtDistanceAndZ(path, 0.0, settings.GroundZ + settings.Height * 0.5);

                if (TryGetOutwardAtDistance(path, 0.0, out var heightTickDirection))
                {
                    heightPoint += heightTickDirection * heightDimOffset;

                    var heightLineBottom = PointAtDistanceAndZ(path, 0.0, settings.GroundZ) + heightTickDirection * heightDimOffset;
                    var heightLineTop = PointAtDistanceAndZ(path, 0.0, settings.GroundZ + settings.Height) + heightTickDirection * heightDimOffset;

                    AddPreviewLine(geometry, heightLineBottom, heightLineTop);
                    AddCenteredTick(geometry, heightLineBottom, heightTickDirection, tickHalfLength);
                    AddCenteredTick(geometry, heightLineTop, heightTickDirection, tickHalfLength);
                }

                geometry.PreviewLabels.Add(new HandrailPreviewLabel(heightPoint, "Height: " + FormatMillimetres(settings.Height)));
            }
        }

        private static bool TryGetOutwardAtDistance(Curve path, double distance, out Vector3d outward)
        {
            outward = Vector3d.Unset;

            var length = path.GetLength();

            if (distance < 0.0)
                distance = 0.0;

            if (distance > length)
                distance = length;

            if (!path.LengthParameter(distance, out var t))
                return false;

            var tangent = path.TangentAt(t);
            tangent.Z = 0.0;

            if (!tangent.Unitize())
                return false;

            outward = Vector3d.CrossProduct(Vector3d.ZAxis, tangent);
            return outward.Unitize();
        }

        private static void AddPreviewLine(HandrailGeometry geometry, Point3d start, Point3d end)
        {
            if (start.DistanceTo(end) <= RhinoMath.ZeroTolerance)
                return;

            geometry.PreviewLines.Add(new HandrailPreviewLine(start, end));
        }

        private static void AddCenteredTick(HandrailGeometry geometry, Point3d centre, Vector3d direction, double halfLength)
        {
            if (!direction.Unitize() || halfLength <= RhinoMath.ZeroTolerance)
                return;

            AddPreviewLine(
                geometry,
                centre - direction * halfLength,
                centre + direction * halfLength
            );
        }

        private static string FormatMillimetres(double value)
        {
            return Math.Round(value).ToString("0") + " mm";
        }

        private static List<Brep> CreateSupportFeetAtPostDistances(Curve path, List<double> postDistances, double widthAlongCurve, double depthPerpendicular, double bottomZ, double topZ)
        {
            var breps = new List<Brep>();

            if (topZ <= bottomZ)
                return breps;

            foreach (var distance in postDistances)
            {
                var brep = CreateVerticalElementAtDistance(path, distance, widthAlongCurve, depthPerpendicular, bottomZ, topZ);

                if (brep != null)
                    breps.Add(brep);
            }

            return breps;
        }

        private static Point3d PointAtDistanceAndZ(Curve path, double distance, double z)
        {
            var length = path.GetLength();

            if (distance < 0.0)
                distance = 0.0;

            if (distance > length)
                distance = length;

            if (!path.LengthParameter(distance, out var t))
            {
                var fallback = path.PointAtStart;
                fallback.Z = z;
                return fallback;
            }

            var point = path.PointAt(t);
            point.Z = z;

            return point;
        }

        private static Brep CreateVerticalElementAtDistance(Curve path, double distance, double widthAlongCurve, double depthPerpendicular, double bottomZ, double topZ)
        {
            var length = path.GetLength();

            if (distance < 0.0)
                distance = 0.0;

            if (distance > length)
                distance = length;

            if (!path.LengthParameter(distance, out var t))
                return null;

            return CreateVerticalElementAtParameter(path, t, widthAlongCurve, depthPerpendicular, bottomZ, topZ);
        }

        private static Brep CreateVerticalElementAtParameter(Curve path, double t, double widthAlongCurve, double depthPerpendicular, double bottomZ, double topZ)
        {
            if (topZ <= bottomZ)
                return null;

            var point = path.PointAt(t);
            point.Z = (bottomZ + topZ) * 0.5;

            var tangent = path.TangentAt(t);
            tangent.Z = 0.0;

            if (!tangent.Unitize())
                return null;

            var zAxis = Vector3d.ZAxis;
            var outward = Vector3d.CrossProduct(zAxis, tangent);

            if (!outward.Unitize())
                return null;

            var plane = new Plane(point, tangent, outward);

            var box = new Box(
                plane,
                new Interval(-widthAlongCurve * 0.5, widthAlongCurve * 0.5),
                new Interval(-depthPerpendicular * 0.5, depthPerpendicular * 0.5),
                new Interval(-(topZ - bottomZ) * 0.5, (topZ - bottomZ) * 0.5)
            );

            return box.ToBrep();
        }

        private static int EnsureLayer(RhinoDoc doc, string name, int parentLayerIndex, System.Drawing.Color? layerColor = null)
        {
            var parentId = Guid.Empty;

            if (parentLayerIndex >= 0)
                parentId = doc.Layers[parentLayerIndex].Id;

            foreach (var layer in doc.Layers)
            {
                if (layer.Name == name && layer.ParentLayerId == parentId)
                {
                    if (layerColor.HasValue && layer.Color != layerColor.Value)
                    {
                        layer.Color = layerColor.Value;
                        doc.Layers.Modify(layer, layer.Index, true);
                    }

                    return layer.Index;
                }
            }

            var newLayer = new Layer
            {
                Name = name,
                ParentLayerId = parentId
            };

            if (layerColor.HasValue)
                newLayer.Color = layerColor.Value;

            return doc.Layers.Add(newLayer);
        }

        private static void ApplyTwoMeterBoxMapping(RhinoObject obj)
        {
            if (obj == null)
                return;

            var mapping = TextureMapping.CreateBoxMapping(
                Plane.WorldXY,
                new Interval(0.0, 2000.0),
                new Interval(0.0, 2000.0),
                new Interval(0.0, 2000.0),
                true
            );

            if (mapping == null)
                return;

            obj.SetTextureMapping(1, mapping, Transform.Identity);
        }

    }
}
