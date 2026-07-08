using System;
using Rhino;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatSpiralStair
{
    internal static class SpiralStairBuilder
    {
        public static SpiralStairGeometry Build(SpiralStairSolution solution, double tolerance)
        {
            var geometry = new SpiralStairGeometry();
            var p = solution.Parameters;

            AddTreadsAndRisers(geometry, solution);
            AddCentreColumn(geometry, solution, tolerance);

            if (p.Mode == SpiralStairMode.Open)
                AddOpenBalustrade(geometry, solution, tolerance);
            else
                AddClosedSkin(geometry, solution);

            AddTopLanding(geometry, solution);
            AddPreviewInfo(geometry, solution);

            return geometry;
        }

        private static void AddTreadsAndRisers(SpiralStairGeometry geometry, SpiralStairSolution solution)
        {
            var p = solution.Parameters;
            var inner = p.InnerRadius;
            var outer = p.Radius;

            for (var i = 0; i < solution.TreadCount; i++)
            {
                var a0 = p.StartAngleRadians + i * solution.SignedStepAngleRadians;
                var a1 = p.StartAngleRadians + (i + 1) * solution.SignedStepAngleRadians;
                var z = p.BaseCenter.Z + i * solution.ActualRiserHeight;

                geometry.Treads.Add(CreateAnnularSectorBox(p.BaseCenter, inner, outer, a0, a1, z, z + p.TreadThickness, 5));

                if (p.Mode == SpiralStairMode.Open)
                {
                    geometry.Treads.Add(CreateRadialLip(p.BaseCenter, inner, outer, a0, z - p.TreadFrontLip, z + p.TreadThickness, 20.0, 4));
                    geometry.Treads.Add(CreateRadialLip(p.BaseCenter, inner, outer, a1, z, z + p.TreadThickness + p.TreadBackLip, 20.0, 4));
                }
                else if (i > 0)
                {
                    var riserZ0 = p.BaseCenter.Z + (i - 1) * solution.ActualRiserHeight + p.TreadThickness;
                    var riserZ1 = p.BaseCenter.Z + i * solution.ActualRiserHeight;
                    geometry.Risers.Add(CreateRadialPanel(p.BaseCenter, inner, outer, a0, riserZ0, riserZ1, 5.0));
                }
            }
        }

        private static void AddCentreColumn(SpiralStairGeometry geometry, SpiralStairSolution solution, double tolerance)
        {
            var p = solution.Parameters;
            var basePoint = p.BaseCenter;
            var topPoint = p.BaseCenter + Vector3d.ZAxis * (p.FloorHeight + p.HandrailHeight);
            var cylinder = new Cylinder(new Circle(new Plane(basePoint, Vector3d.ZAxis), p.ColumnRadius), topPoint.Z - basePoint.Z).ToBrep(true, true);

            if (cylinder != null)
                geometry.CentreColumn.Add(cylinder);
        }

        private static void AddOpenBalustrade(SpiralStairGeometry geometry, SpiralStairSolution solution, double tolerance)
        {
            var p = solution.Parameters;
            var railRadius = p.Radius - 60.0;
            var balusterRadius = p.Radius - 90.0;
            var handrailCurve = CreateHelixCurve(p.BaseCenter, railRadius, p.StartAngleRadians, solution.SignedTotalRotationRadians, p.BaseCenter.Z + p.HandrailHeight, p.BaseCenter.Z + p.FloorHeight + p.HandrailHeight, Math.Max(64, solution.TreadCount * 6));
            AddPipe(geometry.Handrail, handrailCurve, p.HandrailRadius, tolerance);

            for (var i = 0; i < solution.TreadCount; i++)
            {
                var a = p.StartAngleRadians + (i + 0.5) * solution.SignedStepAngleRadians;
                var treadZ = p.BaseCenter.Z + i * solution.ActualRiserHeight + p.TreadThickness;
                var bottom = PointAt(p.BaseCenter, balusterRadius, a, treadZ);
                var top = PointAt(p.BaseCenter, balusterRadius, a, treadZ + p.HandrailHeight);
                AddPipe(geometry.Balusters, new LineCurve(bottom, top), p.BalusterRadius, tolerance);
            }
        }

        private static void AddClosedSkin(SpiralStairGeometry geometry, SpiralStairSolution solution)
        {
            var p = solution.Parameters;
            var baseZ = p.BaseCenter.Z;
            var topZ = p.BaseCenter.Z + p.FloorHeight + p.SolidGuardHeight;
            var skin = CreateHelicalRibbon(p.BaseCenter, p.Radius, p.StartAngleRadians, solution.SignedTotalRotationRadians, baseZ, topZ, Math.Max(80, solution.TreadCount * 8));
            geometry.Skin.Add(skin);
        }

        private static void AddTopLanding(SpiralStairGeometry geometry, SpiralStairSolution solution)
        {
            var p = solution.Parameters;

            if (p.TopLanding == SpiralStairTopLanding.None)
                return;

            var endAngle = p.StartAngleRadians + solution.SignedTotalRotationRadians;
            var z0 = p.BaseCenter.Z + p.FloorHeight;
            var z1 = z0 + p.TreadThickness;

            if (p.TopLanding == SpiralStairTopLanding.Pie)
            {
                var a0 = endAngle - solution.SignedStepAngleRadians;
                var a1 = endAngle;
                geometry.Landings.Add(CreateAnnularSectorBox(p.BaseCenter, p.InnerRadius, p.Radius, a0, a1, z0, z1, 5));
                return;
            }

            var radial = UnitVector(endAngle);
            var tangent = Vector3d.CrossProduct(Vector3d.ZAxis, radial);
            if (solution.Parameters.Direction == SpiralStairDirection.Clockwise)
                tangent.Reverse();

            var width = p.Radius - p.InnerRadius;
            var depth = width;
            var centre = p.BaseCenter + radial * (p.InnerRadius + width * 0.5) + tangent * (depth * 0.5);
            geometry.Landings.Add(CreateBoxFromBasis(centre, radial, tangent, width, depth, p.TreadThickness, z0));
        }

        private static void AddPreviewInfo(SpiralStairGeometry geometry, SpiralStairSolution solution)
        {
            var p = solution.Parameters;
            var baseZ = p.BaseCenter.Z;
            var labelPoint = p.BaseCenter + new Vector3d(p.Radius + 350.0, 0.0, p.FloorHeight * 0.5);
            geometry.PreviewLabels.Add(new SpiralStairPreviewLabel(labelPoint, $"{solution.RiserCount} risers @ {solution.ActualRiserHeight:0} mm"));
            geometry.PreviewLabels.Add(new SpiralStairPreviewLabel(labelPoint + Vector3d.ZAxis * 250.0, $"Rotation {solution.TotalRotationDegrees:0} deg"));

            if (!string.IsNullOrWhiteSpace(solution.Warning))
                geometry.PreviewLabels.Add(new SpiralStairPreviewLabel(labelPoint + Vector3d.ZAxis * 500.0, solution.Warning));

            geometry.PreviewLines.Add(new SpiralStairPreviewLine(p.BaseCenter, p.BaseCenter + Vector3d.ZAxis * p.FloorHeight));
            geometry.PreviewLines.Add(new SpiralStairPreviewLine(new Point3d(p.BaseCenter.X, p.BaseCenter.Y, baseZ), new Point3d(p.BaseCenter.X + p.Radius, p.BaseCenter.Y, baseZ)));
        }

        private static Mesh CreateAnnularSectorBox(Point3d center, double inner, double outer, double a0, double a1, double z0, double z1, int segments)
        {
            var mesh = new Mesh();
            var steps = Math.Max(1, segments);

            for (var i = 0; i <= steps; i++)
            {
                var t = i / (double)steps;
                var a = a0 + (a1 - a0) * t;
                mesh.Vertices.Add(PointAt(center, inner, a, z0));
                mesh.Vertices.Add(PointAt(center, outer, a, z0));
                mesh.Vertices.Add(PointAt(center, inner, a, z1));
                mesh.Vertices.Add(PointAt(center, outer, a, z1));
            }

            for (var i = 0; i < steps; i++)
            {
                var b = i * 4;
                var n = b + 4;
                mesh.Faces.AddFace(b + 2, n + 2, n + 3, b + 3);
                mesh.Faces.AddFace(b, b + 1, n + 1, n);
                mesh.Faces.AddFace(b, n, n + 2, b + 2);
                mesh.Faces.AddFace(b + 1, b + 3, n + 3, n + 1);
            }

            var last = steps * 4;
            mesh.Faces.AddFace(0, 2, 3, 1);
            mesh.Faces.AddFace(last, last + 1, last + 3, last + 2);
            mesh.Normals.ComputeNormals();
            mesh.Compact();
            return mesh;
        }

        private static Mesh CreateRadialLip(Point3d center, double inner, double outer, double angle, double z0, double z1, double thicknessAlongArc, int segments)
        {
            var delta = thicknessAlongArc / Math.Max(outer, 1.0);
            return CreateAnnularSectorBox(center, inner, outer, angle - delta * 0.5, angle + delta * 0.5, z0, z1, segments);
        }

        private static Mesh CreateRadialPanel(Point3d center, double inner, double outer, double angle, double z0, double z1, double thicknessAlongArc)
        {
            var delta = thicknessAlongArc / Math.Max(outer, 1.0);
            return CreateAnnularSectorBox(center, inner, outer, angle - delta * 0.5, angle + delta * 0.5, z0, z1, 1);
        }

        private static Mesh CreateHelicalRibbon(Point3d center, double radius, double startAngle, double signedRotation, double z0, double z1, int segments)
        {
            var mesh = new Mesh();
            var height = z1 - z0;
            var guardHeight = 1100.0;

            for (var i = 0; i <= segments; i++)
            {
                var t = i / (double)segments;
                var a = startAngle + signedRotation * t;
                var stairZ = z0 + (height - guardHeight) * t;
                mesh.Vertices.Add(PointAt(center, radius, a, stairZ));
                mesh.Vertices.Add(PointAt(center, radius, a, stairZ + guardHeight));
            }

            for (var i = 0; i < segments; i++)
            {
                var b = i * 2;
                mesh.Faces.AddFace(b, b + 1, b + 3, b + 2);
            }

            mesh.Normals.ComputeNormals();
            mesh.Compact();
            return mesh;
        }

        private static Curve CreateHelixCurve(Point3d center, double radius, double startAngle, double signedRotation, double z0, double z1, int segments)
        {
            var points = new Point3d[segments + 1];

            for (var i = 0; i <= segments; i++)
            {
                var t = i / (double)segments;
                var a = startAngle + signedRotation * t;
                var z = z0 + (z1 - z0) * t;
                points[i] = PointAt(center, radius, a, z);
            }

            return Curve.CreateInterpolatedCurve(points, 3);
        }

        private static void AddPipe(System.Collections.Generic.List<GeometryBase> target, Curve curve, double radius, double tolerance)
        {
            var pipes = Brep.CreatePipe(curve, radius, false, PipeCapMode.Flat, true, tolerance, RhinoMath.ToRadians(5.0));
            if (pipes == null)
                return;

            foreach (var pipe in pipes)
                target.Add(pipe);
        }

        private static Point3d PointAt(Point3d center, double radius, double angle, double z)
        {
            return new Point3d(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius, z);
        }

        private static Vector3d UnitVector(double angle)
        {
            return new Vector3d(Math.Cos(angle), Math.Sin(angle), 0.0);
        }

        private static Mesh CreateBoxFromBasis(Point3d centre, Vector3d xAxis, Vector3d yAxis, double xSize, double ySize, double zSize, double zBase)
        {
            xAxis.Unitize();
            yAxis.Unitize();
            var zAxis = Vector3d.ZAxis;
            var c = new Point3d(centre.X, centre.Y, zBase + zSize * 0.5);
            var x = xAxis * (xSize * 0.5);
            var y = yAxis * (ySize * 0.5);
            var z = zAxis * (zSize * 0.5);

            var mesh = new Mesh();
            mesh.Vertices.Add(c - x - y - z);
            mesh.Vertices.Add(c + x - y - z);
            mesh.Vertices.Add(c + x + y - z);
            mesh.Vertices.Add(c - x + y - z);
            mesh.Vertices.Add(c - x - y + z);
            mesh.Vertices.Add(c + x - y + z);
            mesh.Vertices.Add(c + x + y + z);
            mesh.Vertices.Add(c - x + y + z);
            mesh.Faces.AddFace(0, 1, 2, 3);
            mesh.Faces.AddFace(4, 7, 6, 5);
            mesh.Faces.AddFace(0, 4, 5, 1);
            mesh.Faces.AddFace(1, 5, 6, 2);
            mesh.Faces.AddFace(2, 6, 7, 3);
            mesh.Faces.AddFace(3, 7, 4, 0);
            mesh.Normals.ComputeNormals();
            mesh.Compact();
            return mesh;
        }
    }
}
