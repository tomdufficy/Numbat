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
            AddTreadsRisersAndLanding(geometry, solution);
            AddCentreColumn(geometry, solution);

            if (solution.Parameters.Mode == SpiralStairMode.Open)
                AddOpenBalustrade(geometry, solution, tolerance);
            else
                AddClosedSkinAndSoffit(geometry, solution);

            AddPreviewInfo(geometry, solution);
            return geometry;
        }

        private static void AddTreadsRisersAndLanding(SpiralStairGeometry geometry, SpiralStairSolution solution)
        {
            var p = solution.Parameters;
            var lastIndex = solution.TreadCount - 1;
            var wedgeCount = p.TopLanding == SpiralStairTopLanding.Rectangular ? lastIndex : solution.TreadCount;
            var inner = GetTreadInnerRadius(p);

            for (var i = 0; i < wedgeCount; i++)
            {
                var isFinalPiece = p.TopLanding == SpiralStairTopLanding.None && i == lastIndex;
                var a0 = p.StartAngleRadians + i * solution.SignedStepAngleRadians;
                var a1 = p.StartAngleRadians + (i + 1) * solution.SignedStepAngleRadians;
                var topZ = p.BaseCenter.Z + (i + 1) * solution.ActualRiserHeight;
                var bottomZ = topZ - p.TreadThickness;
                var previousTopZ = i == 0 ? p.BaseCenter.Z : p.BaseCenter.Z + i * solution.ActualRiserHeight;

                geometry.Treads.Add(CreateAnnularSectorBox(p.BaseCenter, inner, p.Radius, a0, a1, bottomZ, topZ, 5));

                if (p.Mode == SpiralStairMode.Open)
                {
                    geometry.FrontLips.Add(CreateRadialPlate(p.BaseCenter, inner, p.Radius, a0, topZ - p.FoldDepth, topZ, p.TreadThickness));

                    if (!isFinalPiece)
                        geometry.RearLips.Add(CreateRadialPlate(p.BaseCenter, inner, p.Radius, a1, topZ, topZ + p.FoldDepth, p.TreadThickness));
                }
                else
                {
                    geometry.Risers.Add(CreateRadialPlate(p.BaseCenter, inner, p.Radius, a0, previousTopZ, bottomZ, p.TreadThickness));
                }
            }

            if (p.TopLanding == SpiralStairTopLanding.Rectangular)
                AddRectangularLanding(geometry, solution);
        }

        private static void AddRectangularLanding(SpiralStairGeometry geometry, SpiralStairSolution solution)
        {
            var p = solution.Parameters;
            var endAngle = p.StartAngleRadians + solution.SignedTotalRotationRadians;
            var radial = UnitVector(endAngle);
            var tangent = Vector3d.CrossProduct(Vector3d.ZAxis, radial);
            if (p.Direction == SpiralStairDirection.Clockwise)
                tangent.Reverse();

            var inner = GetTreadInnerRadius(p);
            var width = p.Radius - inner;
            var depth = Math.Max(100.0, p.LandingDepth);
            var topZ = p.BaseCenter.Z + p.FloorHeight;
            var bottomZ = topZ - p.TreadThickness;
            var centre = p.BaseCenter + radial * (inner + width * 0.5) + tangent * (depth * 0.5);

            geometry.Landings.Add(CreateBoxFromBasis(centre, radial, tangent, width, depth, p.TreadThickness, bottomZ));

            if (p.Mode == SpiralStairMode.Open)
            {
                geometry.FrontLips.Add(CreateBoxFromBasis(centre - tangent * (depth * 0.5), radial, tangent, width, p.TreadThickness, p.FoldDepth, topZ - p.FoldDepth));
            }
            else
            {
                var previousTopZ = p.BaseCenter.Z + (solution.TreadCount - 1) * solution.ActualRiserHeight;
                var riserHeight = Math.Max(p.TreadThickness, bottomZ - previousTopZ);
                geometry.Risers.Add(CreateBoxFromBasis(centre - tangent * (depth * 0.5), radial, tangent, width, p.TreadThickness, riserHeight, previousTopZ));
            }
        }

        private static void AddCentreColumn(SpiralStairGeometry geometry, SpiralStairSolution solution)
        {
            var p = solution.Parameters;
            var height = p.FloorHeight + p.HandrailHeight + p.HandrailRadius;
            var cylinder = new Cylinder(new Circle(new Plane(p.BaseCenter, Vector3d.ZAxis), p.ColumnRadius), height).ToBrep(true, true);
            if (cylinder != null)
                geometry.CentreColumn.Add(cylinder);
        }

        private static void AddOpenBalustrade(SpiralStairGeometry geometry, SpiralStairSolution solution, double tolerance)
        {
            var p = solution.Parameters;
            var balusterRadiusFromCenter = Math.Max(GetTreadInnerRadius(p) + 50.0, p.Radius - p.BalusterInsetFromOuterEdge);
            var firstAngle = p.StartAngleRadians + solution.SignedStepAngleRadians / 3.0;
            var lastAngle = p.StartAngleRadians + solution.SignedTotalRotationRadians;
            var firstRailZ = HandrailZAtAngle(solution, firstAngle);
            var lastRailZ = HandrailZAtAngle(solution, lastAngle);

            var handrail = CreateHelixCurve(p.BaseCenter, balusterRadiusFromCenter, firstAngle, lastAngle - firstAngle, firstRailZ, lastRailZ, Math.Max(32, solution.TreadCount * 8));
            AddPipe(geometry.Handrail, handrail, p.HandrailRadius, tolerance);

            var balusterNumber = 0;
            var totalBalusters = solution.TreadCount * 3;
            for (var i = 0; i < solution.TreadCount; i++)
            {
                var topZ = p.BaseCenter.Z + (i + 1) * solution.ActualRiserHeight;
                for (var j = 1; j <= 3; j++)
                {
                    balusterNumber++;
                    var t = j / 3.0;
                    var angle = p.StartAngleRadians + (i + t) * solution.SignedStepAngleRadians;
                    var bottom = PointAt(p.BaseCenter, balusterRadiusFromCenter, angle, topZ);
                    var top = PointAt(p.BaseCenter, balusterRadiusFromCenter, angle, HandrailZAtAngle(solution, angle));
                    var radius = balusterNumber == 1 || balusterNumber == totalBalusters ? p.HandrailRadius : p.BalusterRadius;
                    AddPipe(geometry.Balusters, new LineCurve(bottom, top), radius, tolerance);
                }
            }
        }

        private static double HandrailZAtAngle(SpiralStairSolution solution, double angle)
        {
            var p = solution.Parameters;
            var signedFromStart = angle - p.StartAngleRadians;
            var t = Math.Abs(solution.SignedTotalRotationRadians) < 1e-9 ? 0.0 : signedFromStart / solution.SignedTotalRotationRadians;
            if (t < 0.0) t = 0.0;
            if (t > 1.0) t = 1.0;
            return p.BaseCenter.Z + p.FloorHeight * t + p.HandrailHeight;
        }

        private static void AddClosedSkinAndSoffit(SpiralStairGeometry geometry, SpiralStairSolution solution)
        {
            var p = solution.Parameters;
            var inner = GetTreadInnerRadius(p);

            geometry.Skin.Add(CreateHelicalRibbon(
                p.BaseCenter,
                p.Radius,
                p.StartAngleRadians,
                solution.SignedTotalRotationRadians,
                p.BaseCenter.Z,
                p.BaseCenter.Z + p.FloorHeight,
                p.SolidGuardHeight,
                Math.Max(80, solution.TreadCount * 10)));

            geometry.Soffit.Add(CreateHelicalSoffit(
                p.BaseCenter,
                inner,
                p.Radius,
                p.StartAngleRadians,
                solution.SignedTotalRotationRadians,
                p.BaseCenter.Z,
                p.BaseCenter.Z + p.FloorHeight - p.TreadThickness,
                Math.Max(80, solution.TreadCount * 10)));
        }

        private static void AddPreviewInfo(SpiralStairGeometry geometry, SpiralStairSolution solution)
        {
            var p = solution.Parameters;
            var labelPoint = p.BaseCenter + new Vector3d(p.Radius + 350.0, 0.0, p.FloorHeight * 0.5);
            geometry.PreviewLabels.Add(new SpiralStairPreviewLabel(labelPoint, $"{solution.RiserCount} risers @ {solution.ActualRiserHeight:0} mm"));
            geometry.PreviewLabels.Add(new SpiralStairPreviewLabel(labelPoint + Vector3d.ZAxis * 250.0, $"Rotation {solution.TotalRotationDegrees:0} deg"));
            if (!string.IsNullOrWhiteSpace(solution.Warning))
                geometry.PreviewLabels.Add(new SpiralStairPreviewLabel(labelPoint + Vector3d.ZAxis * 500.0, solution.Warning));
            geometry.PreviewLines.Add(new SpiralStairPreviewLine(p.BaseCenter, p.BaseCenter + Vector3d.ZAxis * p.FloorHeight));
            geometry.PreviewLines.Add(new SpiralStairPreviewLine(p.BaseCenter, p.BaseCenter + UnitVector(p.StartAngleRadians) * p.Radius));
        }

        private static double GetTreadInnerRadius(SpiralStairParameters p)
        {
            return Math.Max(20.0, p.ColumnRadius - 5.0);
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

        private static Mesh CreateRadialPlate(Point3d center, double inner, double outer, double angle, double z0, double z1, double thicknessAlongArc)
        {
            if (z1 < z0)
            {
                var temp = z0;
                z0 = z1;
                z1 = temp;
            }

            var delta = thicknessAlongArc / Math.Max(outer, 1.0);
            return CreateAnnularSectorBox(center, inner, outer, angle - delta * 0.5, angle + delta * 0.5, z0, z1, 1);
        }

        private static Mesh CreateHelicalRibbon(Point3d center, double radius, double startAngle, double signedRotation, double bottomZ0, double bottomZ1, double height, int segments)
        {
            var mesh = new Mesh();
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (double)segments;
                var a = startAngle + signedRotation * t;
                var z = bottomZ0 + (bottomZ1 - bottomZ0) * t;
                mesh.Vertices.Add(PointAt(center, radius, a, z));
                mesh.Vertices.Add(PointAt(center, radius, a, z + height));
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

        private static Mesh CreateHelicalSoffit(Point3d center, double inner, double outer, double startAngle, double signedRotation, double z0, double z1, int segments)
        {
            var mesh = new Mesh();
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (double)segments;
                var a = startAngle + signedRotation * t;
                var z = z0 + (z1 - z0) * t;
                mesh.Vertices.Add(PointAt(center, inner, a, z));
                mesh.Vertices.Add(PointAt(center, outer, a, z));
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
                points[i] = PointAt(center, radius, startAngle + signedRotation * t, z0 + (z1 - z0) * t);
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
            var c = new Point3d(centre.X, centre.Y, zBase + zSize * 0.5);
            var x = xAxis * (xSize * 0.5);
            var y = yAxis * (ySize * 0.5);
            var z = Vector3d.ZAxis * (zSize * 0.5);
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
