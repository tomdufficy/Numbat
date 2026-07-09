using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatPV
{
    internal class PVBlockGeometry
    {
        public readonly List<Brep> PVSurface = new List<Brep>();
        public readonly List<Brep> Frame = new List<Brep>();
        public readonly List<GeometryBase> Stand = new List<GeometryBase>();

        public IEnumerable<GeometryBase> AllGeometry()
        {
            foreach (var item in PVSurface) yield return item;
            foreach (var item in Frame) yield return item;
            foreach (var item in Stand) yield return item;
        }
    }

    internal static class PVBuilder
    {
        public static PVBlockGeometry CreatePanelBlockGeometry(PVParameters parameters)
        {
            parameters.Clamp();

            var geometry = new PVBlockGeometry();
            var tiltRadians = Rhino.RhinoMath.ToRadians(parameters.TiltAngleDegrees);
            var hinge = new Point3d(0.0, -parameters.PanelLength * 0.5, parameters.StandHeight);
            var tilt = Transform.Rotation(tiltRadians, Vector3d.XAxis, hinge);

            AddBox(geometry.PVSurface,
                -parameters.PanelWidth * 0.5 + parameters.FrameWidth,
                parameters.PanelWidth * 0.5 - parameters.FrameWidth,
                -parameters.PanelLength * 0.5 + parameters.FrameWidth,
                parameters.PanelLength * 0.5 - parameters.FrameWidth,
                parameters.StandHeight,
                parameters.StandHeight + parameters.PanelThickness,
                tilt);

            var halfWidth = parameters.PanelWidth * 0.5;
            var halfLength = parameters.PanelLength * 0.5;
            var frameWidth = Math.Min(parameters.FrameWidth, Math.Min(parameters.PanelWidth, parameters.PanelLength) * 0.25);

            AddBox(geometry.Frame, -halfWidth, halfWidth, -halfLength, -halfLength + frameWidth, parameters.StandHeight, parameters.StandHeight + parameters.FrameDepth, tilt);
            AddBox(geometry.Frame, -halfWidth, halfWidth, halfLength - frameWidth, halfLength, parameters.StandHeight, parameters.StandHeight + parameters.FrameDepth, tilt);
            AddBox(geometry.Frame, -halfWidth, -halfWidth + frameWidth, -halfLength, halfLength, parameters.StandHeight, parameters.StandHeight + parameters.FrameDepth, tilt);
            AddBox(geometry.Frame, halfWidth - frameWidth, halfWidth, -halfLength, halfLength, parameters.StandHeight, parameters.StandHeight + parameters.FrameDepth, tilt);

            var standRailDepth = Math.Max(10.0, parameters.FrameDepth);
            var standRailWidth = Math.Max(10.0, parameters.FrameWidth * 2.0);
            var railInset = Math.Max(parameters.FrameWidth * 2.0, Math.Min(parameters.PanelLength * 0.12, 160.0));
            var lowerRailCenterY = -halfLength + railInset;
            var upperRailCenterY = halfLength - railInset;
            var lowerRailY0 = lowerRailCenterY - standRailWidth * 0.5;
            var lowerRailY1 = lowerRailCenterY + standRailWidth * 0.5;
            var upperRailY0 = upperRailCenterY - standRailWidth * 0.5;
            var upperRailY1 = upperRailCenterY + standRailWidth * 0.5;
            var railX0 = -halfWidth + parameters.FrameWidth;
            var railX1 = halfWidth - parameters.FrameWidth;
            var railBottomZ = parameters.StandHeight - standRailDepth;
            var railTopZ = parameters.StandHeight;

            AddBox(geometry.Stand, railX0, railX1, lowerRailY0, lowerRailY1, railBottomZ, railTopZ, tilt);
            AddBox(geometry.Stand, railX0, railX1, upperRailY0, upperRailY1, railBottomZ, railTopZ, tilt);

            var standPlateWidth = Math.Max(10.0, parameters.FrameWidth);
            var sideOffset = parameters.PanelWidth * 0.32;
            var supportBaseLength = Math.Max(120.0, standRailWidth * 2.0);

            geometry.Stand.Add(CreateTriangularStandUnderRail(-sideOffset - standPlateWidth * 0.5, -sideOffset + standPlateWidth * 0.5, lowerRailCenterY, railBottomZ, supportBaseLength, tilt));
            geometry.Stand.Add(CreateTriangularStandUnderRail(sideOffset - standPlateWidth * 0.5, sideOffset + standPlateWidth * 0.5, lowerRailCenterY, railBottomZ, supportBaseLength, tilt));
            geometry.Stand.Add(CreateTriangularStandUnderRail(-sideOffset - standPlateWidth * 0.5, -sideOffset + standPlateWidth * 0.5, upperRailCenterY, railBottomZ, supportBaseLength, tilt));
            geometry.Stand.Add(CreateTriangularStandUnderRail(sideOffset - standPlateWidth * 0.5, sideOffset + standPlateWidth * 0.5, upperRailCenterY, railBottomZ, supportBaseLength, tilt));

            return geometry;
        }

        public static List<GeometryBase> CreatePreviewGeometry(PVParameters parameters, IEnumerable<Transform> transforms)
        {
            var blockGeometry = CreatePanelBlockGeometry(parameters);
            var preview = new List<GeometryBase>();

            foreach (var transform in transforms)
            {
                foreach (var geometry in blockGeometry.AllGeometry())
                {
                    var copy = geometry.Duplicate();
                    if (copy == null)
                        continue;
                    copy.Transform(transform);
                    preview.Add(copy);
                }
            }

            return preview;
        }

        private static void AddBox(ICollection<Brep> target, double x0, double x1, double y0, double y1, double z0, double z1, Transform transform)
        {
            var brep = CreateBox(x0, x1, y0, y1, z0, z1, transform);
            if (brep != null)
                target.Add(brep);
        }

        private static void AddBox(ICollection<GeometryBase> target, double x0, double x1, double y0, double y1, double z0, double z1, Transform transform)
        {
            var brep = CreateBox(x0, x1, y0, y1, z0, z1, transform);
            if (brep != null)
                target.Add(brep);
        }

        private static Brep CreateBox(double x0, double x1, double y0, double y1, double z0, double z1, Transform transform)
        {
            var box = new Box(Plane.WorldXY, new Interval(x0, x1), new Interval(y0, y1), new Interval(z0, z1));
            var brep = box.ToBrep();
            if (brep == null)
                return null;
            brep.Transform(transform);
            return brep;
        }

        private static Mesh CreateTriangularStandUnderRail(double x0, double x1, double railCenterY, double railBottomZ, double baseLength, Transform tilt)
        {
            var top = new Point3d(0.0, railCenterY, railBottomZ);
            top.Transform(tilt);

            var halfBase = baseLength * 0.5;
            var y0 = top.Y - halfBase;
            var y1 = top.Y + halfBase;
            var z0 = 0.0;
            var z1 = Math.Max(10.0, top.Z);

            var mesh = new Mesh();
            mesh.Vertices.Add(x0, y0, z0);
            mesh.Vertices.Add(x0, y1, z0);
            mesh.Vertices.Add(x0, top.Y, z1);
            mesh.Vertices.Add(x1, y0, z0);
            mesh.Vertices.Add(x1, y1, z0);
            mesh.Vertices.Add(x1, top.Y, z1);

            mesh.Faces.AddFace(0, 1, 2);
            mesh.Faces.AddFace(3, 5, 4);
            mesh.Faces.AddFace(0, 3, 4, 1);
            mesh.Faces.AddFace(1, 4, 5, 2);
            mesh.Faces.AddFace(2, 5, 3, 0);
            mesh.Normals.ComputeNormals();
            mesh.Compact();
            return mesh;
        }
    }
}
