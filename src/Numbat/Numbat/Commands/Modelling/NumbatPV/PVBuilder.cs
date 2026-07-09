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

            var standPlateWidth = Math.Max(20.0, parameters.FrameWidth);
            var sideOffset = parameters.PanelWidth * 0.32;
            geometry.Stand.Add(CreateTriangularStand(-sideOffset - standPlateWidth * 0.5, -sideOffset + standPlateWidth * 0.5, parameters));
            geometry.Stand.Add(CreateTriangularStand(sideOffset - standPlateWidth * 0.5, sideOffset + standPlateWidth * 0.5, parameters));

            var standRail = CreateBox(
                -sideOffset,
                sideOffset,
                -parameters.PanelLength * 0.32,
                -parameters.PanelLength * 0.32 + Math.Max(20.0, parameters.FrameWidth),
                parameters.StandHeight * 0.45,
                parameters.StandHeight * 0.45 + Math.Max(20.0, parameters.FrameWidth),
                Transform.Identity);
            if (standRail != null)
                geometry.Stand.Add(standRail);

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

        private static Brep CreateBox(double x0, double x1, double y0, double y1, double z0, double z1, Transform transform)
        {
            var box = new Box(Plane.WorldXY, new Interval(x0, x1), new Interval(y0, y1), new Interval(z0, z1));
            var brep = box.ToBrep();
            if (brep == null)
                return null;
            brep.Transform(transform);
            return brep;
        }

        private static Mesh CreateTriangularStand(double x0, double x1, PVParameters parameters)
        {
            var y0 = -parameters.PanelLength * 0.38;
            var y1 = parameters.PanelLength * 0.12;
            var z0 = 0.0;
            var z1 = parameters.StandHeight;

            var mesh = new Mesh();
            mesh.Vertices.Add(x0, y0, z0);
            mesh.Vertices.Add(x0, y1, z0);
            mesh.Vertices.Add(x0, y1, z1);
            mesh.Vertices.Add(x1, y0, z0);
            mesh.Vertices.Add(x1, y1, z0);
            mesh.Vertices.Add(x1, y1, z1);

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
