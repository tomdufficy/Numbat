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

            if (!IsCurveFlatInZ(originalCurve, doc.ModelAbsoluteTolerance, out var groundZ))
            {
                RhinoApp.WriteLine("Input curve must be flat/planar in Z.");
                return Result.Failure;
            }

            var height = new OptionDouble(1100.0, true, 100.0);
            var railDepth = new OptionDouble(40.0, true, 1.0);
            var railHeight = new OptionDouble(20.0, true, 1.0);
            var balusterWidth = new OptionDouble(10.0, true, 1.0);
            var balusterDepth = new OptionDouble(20.0, true, 1.0);
            var maxBalusterSpacing = new OptionDouble(100.0, true, 10.0);

            var bottomRailRaised = new OptionToggle(true, "Ground", "Raised");
            var bottomRailHeight = new OptionDouble(100.0, true, 0.0);
            var supportFeet = new OptionToggle(true, "No", "Yes");

            var wallTabs = new OptionToggle(false, "No", "Yes");
            var tabLength = new OptionDouble(100.0, true, 1.0);

            var settings = new HandrailSettings();
            var conduit = new HandrailPreviewConduit();
            conduit.Enabled = true;

            try
            {
                while (true)
                {
                    settings.Height = height.CurrentValue;
                    settings.RailDepth = railDepth.CurrentValue;
                    settings.RailHeight = railHeight.CurrentValue;
                    settings.BalusterWidth = balusterWidth.CurrentValue;
                    settings.BalusterDepth = balusterDepth.CurrentValue;
                    settings.MaxBalusterSpacing = maxBalusterSpacing.CurrentValue;
                    settings.BottomRailRaised = bottomRailRaised.CurrentValue;
                    settings.BottomRailHeight = bottomRailHeight.CurrentValue;
                    settings.SupportFeet = bottomRailRaised.CurrentValue && bottomRailHeight.CurrentValue > RhinoMath.ZeroTolerance && supportFeet.CurrentValue;
                    settings.WallTabs = wallTabs.CurrentValue;
                    settings.TabLength = tabLength.CurrentValue;
                    settings.GroundZ = groundZ;
                    settings.SupportSpacing = 1000.0;

                    var previewGeometry = CreateHandrailGeometry(originalCurve, settings, doc.ModelAbsoluteTolerance);
                    conduit.PreviewBreps = previewGeometry.AllBreps();
                    doc.Views.Redraw();

                    var getOptions = new GetOption();
                    getOptions.SetCommandPrompt("Handrail options. Press Enter to create handrail");
                    getOptions.AcceptNothing(true);

                    getOptions.AddOptionDouble("Height", ref height);
                    getOptions.AddOptionDouble("RailDepth", ref railDepth);
                    getOptions.AddOptionDouble("RailHeight", ref railHeight);
                    getOptions.AddOptionDouble("BalusterWidth", ref balusterWidth);
                    getOptions.AddOptionDouble("BalusterDepth", ref balusterDepth);
                    getOptions.AddOptionDouble("MaxBalusterSpacing", ref maxBalusterSpacing);
                    getOptions.AddOptionToggle("BottomRail", ref bottomRailRaised);

                    if (bottomRailRaised.CurrentValue)
                    {
                        getOptions.AddOptionDouble("BottomRailHeight", ref bottomRailHeight);

                        if (bottomRailHeight.CurrentValue > RhinoMath.ZeroTolerance)
                            getOptions.AddOptionToggle("SupportFeet", ref supportFeet);
                    }

                    getOptions.AddOptionToggle("WallTabs", ref wallTabs);

                    if (wallTabs.CurrentValue)
                        getOptions.AddOptionDouble("TabLength", ref tabLength);

                    var result = getOptions.Get();

                    if (result == GetResult.Nothing)
                        break;

                    if (result == GetResult.Cancel)
                        return Result.Cancel;
                }
            }
            finally
            {
                conduit.Enabled = false;
                doc.Views.Redraw();
            }

            settings.Height = height.CurrentValue;
            settings.RailDepth = railDepth.CurrentValue;
            settings.RailHeight = railHeight.CurrentValue;
            settings.BalusterWidth = balusterWidth.CurrentValue;
            settings.BalusterDepth = balusterDepth.CurrentValue;
            settings.MaxBalusterSpacing = maxBalusterSpacing.CurrentValue;
            settings.BottomRailRaised = bottomRailRaised.CurrentValue;
            settings.BottomRailHeight = bottomRailHeight.CurrentValue;
            settings.SupportFeet = bottomRailRaised.CurrentValue && bottomRailHeight.CurrentValue > RhinoMath.ZeroTolerance && supportFeet.CurrentValue;
            settings.WallTabs = wallTabs.CurrentValue;
            settings.TabLength = tabLength.CurrentValue;
            settings.GroundZ = groundZ;
            settings.SupportSpacing = 1000.0;

            var finalGeometry = CreateHandrailGeometry(originalCurve, settings, doc.ModelAbsoluteTolerance);
            AddGeometryToDocument(doc, finalGeometry);

            doc.Views.Redraw();

            RhinoApp.WriteLine("nbHandrail created.");
            RhinoApp.WriteLine($"Height: {settings.Height}");
            RhinoApp.WriteLine($"Rail section: {settings.RailDepth} x {settings.RailHeight}");
            RhinoApp.WriteLine($"Baluster section: {settings.BalusterWidth} x {settings.BalusterDepth}");
            RhinoApp.WriteLine($"Max baluster spacing: {settings.MaxBalusterSpacing}");
            RhinoApp.WriteLine($"Bottom rail: {(settings.BottomRailRaised ? "Raised" : "Ground")}");
            RhinoApp.WriteLine($"Support feet: {(settings.SupportFeet ? "Yes" : "No")}");
            RhinoApp.WriteLine($"Wall tabs: {(settings.WallTabs ? "Yes" : "No")}");

            return Result.Success;
        }

        private static bool IsCurveFlatInZ(Curve curve, double tolerance, out double groundZ)
        {
            var bbox = curve.GetBoundingBox(true);
            groundZ = bbox.Min.Z;

            return Math.Abs(bbox.Max.Z - bbox.Min.Z) <= tolerance;
        }

        private static HandrailGeometry CreateHandrailGeometry(Curve originalCurve, HandrailSettings settings, double tolerance)
        {
            var geometry = new HandrailGeometry();

            var workingCurve = originalCurve.DuplicateCurve();
            var originalStartCurve = default(Curve);
            var originalEndCurve = default(Curve);

            if (settings.WallTabs)
            {
                if (workingCurve.GetLength() <= settings.TabLength * 2.0)
                    return geometry;

                if (!workingCurve.LengthParameter(settings.TabLength, out var startT))
                    return geometry;

                if (!workingCurve.LengthParameter(workingCurve.GetLength() - settings.TabLength, out var endT))
                    return geometry;

                originalStartCurve = workingCurve.Trim(workingCurve.Domain.Min, startT);
                originalEndCurve = workingCurve.Trim(endT, workingCurve.Domain.Max);
                workingCurve = workingCurve.Trim(startT, endT);

                if (workingCurve == null)
                    return geometry;
            }

            var bottomRailBottomZ = settings.BottomRailRaised
                ? settings.GroundZ + settings.BottomRailHeight
                : settings.GroundZ;

            var bottomRailCenterZ = bottomRailBottomZ + settings.RailHeight * 0.5;
            var topRailCenterZ = settings.GroundZ + settings.Height - settings.RailHeight * 0.5;

            var bottomRailCurve = MoveCurveToZ(workingCurve, bottomRailCenterZ);
            var topRailCurve = MoveCurveToZ(workingCurve, topRailCenterZ);

            geometry.BottomRails.AddRange(CreateSweptRectangularRail(bottomRailCurve, settings.RailDepth, settings.RailHeight, tolerance));
            geometry.TopRails.AddRange(CreateSweptRectangularRail(topRailCurve, settings.RailDepth, settings.RailHeight, tolerance));

            if (settings.BottomRailRaised && settings.SupportFeet)
            {
                var supports = CreateVerticalElementsAlongCurve(
                    workingCurve,
                    settings.RailHeight,
                    settings.RailDepth,
                    settings.GroundZ,
                    bottomRailBottomZ,
                    settings.SupportSpacing
                );

                geometry.Supports.AddRange(supports);
            }

            geometry.EndPosts.Add(CreateVerticalElementAtCurveEnd(
                workingCurve,
                true,
                settings.RailHeight,
                settings.RailDepth,
                bottomRailBottomZ,
                settings.GroundZ + settings.Height
            ));

            geometry.EndPosts.Add(CreateVerticalElementAtCurveEnd(
                workingCurve,
                false,
                settings.RailHeight,
                settings.RailDepth,
                bottomRailBottomZ,
                settings.GroundZ + settings.Height
            ));

            geometry.Balusters.AddRange(CreateBalusters(
                workingCurve,
                settings.BalusterWidth,
                settings.BalusterDepth,
                bottomRailBottomZ + settings.RailHeight,
                settings.GroundZ + settings.Height - settings.RailHeight,
                settings.MaxBalusterSpacing
            ));

            if (settings.WallTabs)
            {
                var upperTabZ = settings.GroundZ + settings.Height - 75.0;
                var lowerTabZ = bottomRailBottomZ + 75.0;

                if (originalStartCurve != null)
                {
                    geometry.WallTabs.AddRange(CreateSweptRectangularRail(MoveCurveToZ(originalStartCurve, upperTabZ), settings.RailDepth, settings.RailHeight, tolerance));
                    geometry.WallTabs.AddRange(CreateSweptRectangularRail(MoveCurveToZ(originalStartCurve, lowerTabZ), settings.RailDepth, settings.RailHeight, tolerance));
                }

                if (originalEndCurve != null)
                {
                    geometry.WallTabs.AddRange(CreateSweptRectangularRail(MoveCurveToZ(originalEndCurve, upperTabZ), settings.RailDepth, settings.RailHeight, tolerance));
                    geometry.WallTabs.AddRange(CreateSweptRectangularRail(MoveCurveToZ(originalEndCurve, lowerTabZ), settings.RailDepth, settings.RailHeight, tolerance));
                }
            }

            return geometry;
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

        private static List<Brep> CreateBalusters(
            Curve path,
            double widthAlongCurve,
            double depthPerpendicular,
            double bottomZ,
            double topZ,
            double maxSpacing
        )
        {
            var breps = new List<Brep>();
            var length = path.GetLength();

            if (length <= RhinoMath.ZeroTolerance)
                return breps;

            var divisionCount = Math.Max(1, (int)Math.Ceiling(length / maxSpacing));
            var spacing = length / divisionCount;

            for (var i = 1; i < divisionCount; i++)
            {
                var distance = i * spacing;

                if (!path.LengthParameter(distance, out var t))
                    continue;

                var brep = CreateVerticalElementAtParameter(
                    path,
                    t,
                    widthAlongCurve,
                    depthPerpendicular,
                    bottomZ,
                    topZ
                );

                if (brep != null)
                    breps.Add(brep);
            }

            return breps;
        }

        private static List<Brep> CreateVerticalElementsAlongCurve(
            Curve path,
            double widthAlongCurve,
            double depthPerpendicular,
            double bottomZ,
            double topZ,
            double maxSpacing
        )
        {
            var breps = new List<Brep>();
            var length = path.GetLength();

            if (length <= RhinoMath.ZeroTolerance)
                return breps;

            var divisionCount = Math.Max(1, (int)Math.Ceiling(length / maxSpacing));
            var spacing = length / divisionCount;

            for (var i = 0; i <= divisionCount; i++)
            {
                var distance = i * spacing;

                if (distance > length)
                    distance = length;

                if (!path.LengthParameter(distance, out var t))
                    continue;

                var brep = CreateVerticalElementAtParameter(
                    path,
                    t,
                    widthAlongCurve,
                    depthPerpendicular,
                    bottomZ,
                    topZ
                );

                if (brep != null)
                    breps.Add(brep);
            }

            return breps;
        }

        private static Brep CreateVerticalElementAtCurveEnd(
            Curve path,
            bool start,
            double widthAlongCurve,
            double depthPerpendicular,
            double bottomZ,
            double topZ
        )
        {
            var t = start ? path.Domain.Min : path.Domain.Max;

            return CreateVerticalElementAtParameter(
                path,
                t,
                widthAlongCurve,
                depthPerpendicular,
                bottomZ,
                topZ
            );
        }

        private static Brep CreateVerticalElementAtParameter(
            Curve path,
            double t,
            double widthAlongCurve,
            double depthPerpendicular,
            double bottomZ,
            double topZ
        )
        {
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

        private static void AddGeometryToDocument(RhinoDoc doc, HandrailGeometry geometry)
        {
            var parentLayerIndex = EnsureLayer(doc, "nbHandrail", -1);

            var topRailLayer = EnsureLayer(doc, "Top Rails", parentLayerIndex);
            var bottomRailLayer = EnsureLayer(doc, "Bottom Rails", parentLayerIndex);
            var balusterLayer = EnsureLayer(doc, "Balusters", parentLayerIndex);
            var endPostLayer = EnsureLayer(doc, "End Posts", parentLayerIndex);
            var supportLayer = EnsureLayer(doc, "Bottom Supports", parentLayerIndex);
            var wallTabLayer = EnsureLayer(doc, "Wall Tabs", parentLayerIndex);

            AddBrepsToLayer(doc, geometry.TopRails, topRailLayer);
            AddBrepsToLayer(doc, geometry.BottomRails, bottomRailLayer);
            AddBrepsToLayer(doc, geometry.Balusters, balusterLayer);
            AddBrepsToLayer(doc, geometry.EndPosts, endPostLayer);
            AddBrepsToLayer(doc, geometry.Supports, supportLayer);
            AddBrepsToLayer(doc, geometry.WallTabs, wallTabLayer);
        }

        private static int EnsureLayer(RhinoDoc doc, string name, int parentLayerIndex)
        {
            var parentId = Guid.Empty;

            if (parentLayerIndex >= 0)
                parentId = doc.Layers[parentLayerIndex].Id;

            foreach (var layer in doc.Layers)
            {
                if (layer.Name == name && layer.ParentLayerId == parentId)
                    return layer.Index;
            }

            var newLayer = new Layer
            {
                Name = name,
                ParentLayerId = parentId
            };

            return doc.Layers.Add(newLayer);
        }

        private static void AddBrepsToLayer(RhinoDoc doc, List<Brep> breps, int layerIndex)
        {
            foreach (var brep in breps)
            {
                var attributes = new ObjectAttributes
                {
                    LayerIndex = layerIndex
                };

                doc.Objects.AddBrep(brep, attributes);
            }
        }

        private class HandrailSettings
        {
            public double Height { get; set; }
            public double RailDepth { get; set; }
            public double RailHeight { get; set; }
            public double BalusterWidth { get; set; }
            public double BalusterDepth { get; set; }
            public double MaxBalusterSpacing { get; set; }
            public bool BottomRailRaised { get; set; }
            public double BottomRailHeight { get; set; }
            public bool SupportFeet { get; set; }
            public bool WallTabs { get; set; }
            public double TabLength { get; set; }
            public double GroundZ { get; set; }
            public double SupportSpacing { get; set; }
        }

        private class HandrailGeometry
        {
            public List<Brep> TopRails { get; } = new List<Brep>();
            public List<Brep> BottomRails { get; } = new List<Brep>();
            public List<Brep> Balusters { get; } = new List<Brep>();
            public List<Brep> EndPosts { get; } = new List<Brep>();
            public List<Brep> Supports { get; } = new List<Brep>();
            public List<Brep> WallTabs { get; } = new List<Brep>();

            public List<Brep> AllBreps()
            {
                var all = new List<Brep>();

                all.AddRange(TopRails);
                all.AddRange(BottomRails);
                all.AddRange(Balusters);
                all.AddRange(EndPosts);
                all.AddRange(Supports);
                all.AddRange(WallTabs);

                return all;
            }
        }

        private class HandrailPreviewConduit : DisplayConduit
        {
            public List<Brep> PreviewBreps { get; set; } = new List<Brep>();

            protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
            {
                foreach (var brep in PreviewBreps)
                    e.IncludeBoundingBox(brep.GetBoundingBox(true));
            }

            protected override void DrawForeground(DrawEventArgs e)
            {
                var previewColor = System.Drawing.Color.FromArgb(246, 217, 245);
                var material = new DisplayMaterial(previewColor, 0.35);

                foreach (var brep in PreviewBreps)
                {
                    e.Display.DrawBrepShaded(brep, material);
                    e.Display.DrawBrepWires(brep, previewColor, 1);
                }
            }
        }
    }
}