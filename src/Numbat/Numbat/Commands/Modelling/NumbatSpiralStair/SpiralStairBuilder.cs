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
            var inner = GetTreadInnerRadius(p);

            for (var i = 0; i < solution.TreadCount; i++)
            {
                var isFinalPiece = i == lastIndex;
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
            var firstAngle = p.StartAngleRadians;
            var lastAngle = p.StartAngleRadians + solution.SignedTotalRotationRadians;

            var extension = HelixAngleForLength(balusterRadiusFromCenter, solution, 50.0);
            var startRailAngle = firstAngle - Math.Sign(solution.SignedTotalRotationRadians) * extension;
            var endRailAngle = lastAngle + Math.Sign(solution.SignedTotalRotationRadians) * extension;
            var firstRailZ = HandrailZAtAngle(solution, startRailAngle, false);
            var lastRailZ = HandrailZAtAngle(solution, endRailAngle, false);

            var handrail = CreateHelixCurve(p.BaseCenter, balusterRadiusFromCenter, startRailAngle, endRailAngle - startRailAngle, firstRailZ, lastRailZ, Math.Max(32, solution.TreadCount * 8));
            AddPipe(geometry.Handrail, handrail, p.HandrailRadius, tolerance);

            var firstBottom = PointAt(p.BaseCenter, balusterRadiusFromCenter, firstAngle, p.BaseCenter.Z);
            var firstTop = PointAt(p.BaseCenter, balusterRadiusFromCenter, firstAngle, HandrailZAtAngle(solution, firstAngle, true));
            AddPipe(geometry.Balusters, new LineCurve(firstBottom, firstTop), p.BalusterRadius, tolerance);

            for (var i = 0; i < solution.TreadCount; i++)
            {
                var topZ = p.BaseCenter.Z + (i + 1) * solution.ActualRiserHeight;
                for (var j = 1; j <= 3; j++)
                {
                    var t = j / 3.0;
                    var angle = p.StartAngleRadians + (i + t) * solution.SignedStepAngleRadians;
                    var bottom = PointAt(p.BaseCenter, balusterRadiusFromCenter, angle, topZ);
                    var top = PointAt(p.BaseCenter, balusterRadiusFromCenter, angle, HandrailZAtAngle(solution, angle, true));
                    AddPipe(geometry.Balusters, new LineCurve(bottom, top), p.BalusterRadius, tolerance);
                }
            }
        }

        private static double HandrailZAtAngle(SpiralStairSolution solution, double angle, bool clamp)
        {
            var p = solution.Parameters;
            var signedFromStart = angle - p.StartAngleRadians;
            var t = Math.Abs(solution.SignedTotalRotationRadians) < 1e-9 ? 0.0 : signedFromStart / solution.SignedTotalRotationRadians;
            if (clamp)
            {
                if (t < 0.0) t = 0.0;
                if (t > 1.0) t = 1.0;
            }
            return p.BaseCenter.Z + p.FloorHeight * t + p.HandrailHeight;
        }

        private static double HelixAngleForLength(double radius, SpiralStairSolution solution, double length)
        {
            var verticalPerRadian = Math.Abs(solution.SignedTotalRotationRadians) < 1e-9 ? 0.0 : solution.Parameters.FloorHeight / Math.Abs(solution.SignedTotalRotationRadians);
            var lengthPerRadian = Math.Sqrt(radius * radius + verticalPerRadian * verticalPerRadian);
            return length / Math.Max(1.0, lengthPerRadian);
        }

        private static void AddClosedSkinAndSoffit(SpiralStairGeometry geometry, SpiralStairSolution solution)
        {
            var p = solution.Parameters;
            var inner = GetTreadInnerRadius(p);

            AddClosedSkin(geometry, solution);

            var skinOuterRadius = GetClosedSkinOuterRadius(p);

            geometry.Soffit.Add(CreateHelicalSoffit(
                p.BaseCenter,
                inner,
                skinOuterRadius,
                p.StartAngleRadians,
                solution.SignedTotalRotationRadians,
                p.BaseCenter.Z,
                p.BaseCenter.Z + p.FloorHeight,
                Math.Max(80, solution.TreadCount * 10)));
        }

        private static void AddClosedSkin(SpiralStairGeometry geometry, SpiralStairSolution solution)
        {
            var p = solution.Parameters;
            var skinThickness = p.ClosedSkinThickness;

            if (Math.Abs(skinThickness) <= 0.001)
            {
                geometry.Skin.Add(CreateHelicalRibbon(
                    p.BaseCenter,
                    p.Radius,
                    p.StartAngleRadians,
                    solution.SignedTotalRotationRadians,
                    p.BaseCenter.Z,
                    p.BaseCenter.Z + p.FloorHeight,
                    p.SolidGuardHeight,
                    Math.Max(80, solution.TreadCount * 10)));
                return;
            }

            if (!p.SplitClosedSkin)
            {
                geometry.Skin.Add(CreateHelicalWall(
                    p.BaseCenter,
                    GetClosedSkinOuterRadius(p),
                    GetClosedSkinInnerRadius(p),
                    p.StartAngleRadians,
                    solution.SignedTotalRotationRadians,
                    p.BaseCenter.Z,
                    p.BaseCenter.Z + p.FloorHeight,
                    p.SolidGuardHeight,
                    Math.Max(80, solution.TreadCount * 10)));
                return;
            }

            const int treadsPerModule = 4;
            const double gapAlongArc = 10.0;
            double sign = Math.Sign(solution.SignedTotalRotationRadians);
            if (Math.Abs(sign) < 0.5)
                sign = 1.0;

            var gapAngle = gapAlongArc / Math.Max(1.0, p.Radius);
            var outerRadius = GetClosedSkinOuterRadius(p);
            var innerRadius = GetClosedSkinInnerRadius(p);

            for (var startTread = 0; startTread < solution.TreadCount; startTread += treadsPerModule)
            {
                var endTread = Math.Min(solution.TreadCount, startTread + treadsPerModule);
                var a0 = p.StartAngleRadians + startTread * solution.SignedStepAngleRadians;
                var a1 = p.StartAngleRadians + endTread * solution.SignedStepAngleRadians;

                if (startTread > 0)
                    a0 += sign * gapAngle * 0.5;

                if (endTread < solution.TreadCount)
                    a1 -= sign * gapAngle * 0.5;

                var t0 = startTread / (double)solution.TreadCount;
                var t1 = endTread / (double)solution.TreadCount;
                var z0 = p.BaseCenter.Z + p.FloorHeight * t0;
                var z1 = p.BaseCenter.Z + p.FloorHeight * t1;

                geometry.Skin.Add(CreateHelicalWall(
                    p.BaseCenter,
                    outerRadius,
                    innerRadius,
                    a0,
                    a1 - a0,
                    z0,
                    z1,
                    p.SolidGuardHeight,
                    Math.Max(12, (endTread - startTread) * 10)));
            }
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

        private static double GetClosedSkinOuterRadius(SpiralStairParameters p)
        {
            if (p.ClosedSkinThickness > 0.001)
                return p.Radius + p.ClosedSkinThickness;

            return p.Radius;
        }

        private static double GetClosedSkinInnerRadius(SpiralStairParameters p)
        {
            if (p.ClosedSkinThickness > 0.001)
                return p.Radius;

            return Math.Max(GetTreadInnerRadius(p), p.Radius + p.ClosedSkinThickness);
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

            var radial = UnitVector(angle);
            var tangent = Vector3d.CrossProduct(Vector3d.ZAxis, radial);
            tangent.Unitize();

            var width = Math.Max(1.0, outer - inner);
            var height = Math.Max(1.0, z1 - z0);
            var centre = center + radial * (inner + width * 0.5);

            return CreateBoxFromBasis(centre, radial, tangent, width, thicknessAlongArc, height, z0);
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

        private static Mesh CreateHelicalWall(Point3d center, double outerRadius, double innerRadius, double startAngle, double signedRotation, double bottomZ0, double bottomZ1, double height, int segments)
        {
            var mesh = new Mesh();
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (double)segments;
                var a = startAngle + signedRotation * t;
                var z = bottomZ0 + (bottomZ1 - bottomZ0) * t;

                mesh.Vertices.Add(PointAt(center, outerRadius, a, z));
                mesh.Vertices.Add(PointAt(center, outerRadius, a, z + height));
                mesh.Vertices.Add(PointAt(center, innerRadius, a, z));
                mesh.Vertices.Add(PointAt(center, innerRadius, a, z + height));
            }

            for (var i = 0; i < segments; i++)
            {
                var b = i * 4;
                var n = b + 4;

                mesh.Faces.AddFace(b, b + 1, n + 1, n);
                mesh.Faces.AddFace(b + 2, n + 2, n + 3, b + 3);
                mesh.Faces.AddFace(b + 1, b + 3, n + 3, n + 1);
                mesh.Faces.AddFace(b, n, n + 2, b + 2);
            }

            var last = segments * 4;
            mesh.Faces.AddFace(0, 2, 3, 1);
            mesh.Faces.AddFace(last, last + 1, last + 3, last + 2);
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
            var pipes = Brep.CreatePipe(curve, radius, false, PipeCapMode.Round, true, tolerance, RhinoMath.ToRadians(5.0));
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
