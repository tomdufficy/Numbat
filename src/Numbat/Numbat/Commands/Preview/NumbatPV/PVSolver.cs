using System;
using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatPV
{
    internal static class PVSolver
    {
        private struct Point2
        {
            public double X;
            public double Y;

            public Point2(double x, double y)
            {
                X = x;
                Y = y;
            }

            public static Point2 operator +(Point2 a, Point2 b) => new Point2(a.X + b.X, a.Y + b.Y);
            public static Point2 operator -(Point2 a, Point2 b) => new Point2(a.X - b.X, a.Y - b.Y);
            public static Point2 operator *(Point2 a, double b) => new Point2(a.X * b, a.Y * b);
        }

        private class RoofFace
        {
            public Plane Plane;
            public List<Point2> Polygon = new List<Point2>();
        }

        private class GridCandidate
        {
            public Vector3d XAxis;
            public Vector3d YAxis;
            public int Count;
        }

        public static PVGeometry Solve(IEnumerable<ObjRef> objects, PVParameters parameters, double tolerance)
        {
            parameters.Clamp();

            var result = new PVGeometry();

            foreach (var objRef in objects)
            {
                if (!TryGetRoofFace(objRef, tolerance, out var roof))
                {
                    result.SkippedCount++;
                    continue;
                }

                result.RoofCount++;
                var transforms = CreatePanelTransforms(roof, parameters, tolerance);
                result.PanelTransforms.AddRange(transforms);
            }

            return result;
        }

        private static bool TryGetRoofFace(ObjRef objRef, double tolerance, out RoofFace roof)
        {
            roof = null;

            var brep = objRef.Brep();
            if (brep != null)
                return TryGetRoofFaceFromBrep(brep, tolerance, out roof);

            var surface = objRef.Surface();
            if (surface != null)
            {
                var surfaceBrep = surface.ToBrep();
                if (surfaceBrep != null)
                    return TryGetRoofFaceFromBrep(surfaceBrep, tolerance, out roof);
            }

            return false;
        }

        private static bool TryGetRoofFaceFromBrep(Brep brep, double tolerance, out RoofFace roof)
        {
            roof = null;
            var bestFaceIndex = -1;
            var bestZ = double.MinValue;
            Plane bestPlane = Plane.Unset;

            for (var i = 0; i < brep.Faces.Count; i++)
            {
                var face = brep.Faces[i];
                if (!face.TryGetPlane(out var plane, tolerance))
                    continue;

                if (plane.ZAxis.Z < 0.0)
                {
                    plane.Flip();
                }

                if (plane.ZAxis.Z < 0.5)
                    continue;

                var box = face.GetBoundingBox(true);
                if (box.Max.Z > bestZ)
                {
                    bestZ = box.Max.Z;
                    bestFaceIndex = i;
                    bestPlane = plane;
                }
            }

            if (bestFaceIndex < 0)
                return false;

            var bestFace = brep.Faces[bestFaceIndex];
            Curve outerCurve = null;
            foreach (var loop in bestFace.Loops)
            {
                if (loop.LoopType == BrepLoopType.Outer)
                {
                    outerCurve = loop.To3dCurve();
                    break;
                }
            }

            if (outerCurve == null)
                return false;

            var polygon = CurveToPlanePolygon(outerCurve, bestPlane, tolerance);
            if (polygon.Count < 3)
                return false;

            roof = new RoofFace
            {
                Plane = bestPlane,
                Polygon = polygon
            };

            return true;
        }

        private static List<Point2> CurveToPlanePolygon(Curve curve, Plane plane, double tolerance)
        {
            var points = new List<Point3d>();

            if (curve.TryGetPolyline(out var polyline))
            {
                foreach (var point in polyline)
                    points.Add(point);
            }
            else
            {
                var count = Math.Max(12, (int)(curve.GetLength() / Math.Max(250.0, tolerance * 10.0)));
                for (var i = 0; i < count; i++)
                {
                    var t = curve.Domain.ParameterAt((double)i / count);
                    points.Add(curve.PointAt(t));
                }
            }

            var polygon = new List<Point2>();
            foreach (var point in points)
            {
                if (!plane.ClosestParameter(point, out var u, out var v))
                    continue;

                var candidate = new Point2(u, v);
                if (polygon.Count == 0 || Distance(candidate, polygon[polygon.Count - 1]) > tolerance)
                    polygon.Add(candidate);
            }

            if (polygon.Count > 1 && Distance(polygon[0], polygon[polygon.Count - 1]) <= tolerance)
                polygon.RemoveAt(polygon.Count - 1);

            return polygon;
        }

        private static List<Transform> CreatePanelTransforms(RoofFace roof, PVParameters parameters, double tolerance)
        {
            var transforms = new List<Transform>();
            var candidate = ChooseBestGridCandidate(roof, parameters, tolerance);
            if (candidate == null || candidate.Count == 0)
                return transforms;

            var xAxis = candidate.XAxis;
            var yAxis = candidate.YAxis;
            if (parameters.Rotate90)
            {
                var swap = xAxis;
                xAxis = yAxis;
                yAxis = -swap;
            }

            OrientTiltTowardSouth(roof.Plane, ref xAxis, ref yAxis);

            var polygon = ProjectPolygonToAxes(roof, xAxis, yAxis);
            var xValues = GetProjectedRange(polygon, true);
            var yValues = GetProjectedRange(polygon, false);

            var xMin = xValues.Item1 + parameters.ParapetMargin;
            var xMax = xValues.Item2 - parameters.ParapetMargin;
            var yMin = yValues.Item1 + parameters.ParapetMargin;
            var yMax = yValues.Item2 - parameters.ParapetMargin;

            var xStep = parameters.PanelWidth + parameters.PanelGap;
            var yStep = parameters.PanelLength + parameters.RowGap;
            var xCount = (int)Math.Floor((xMax - xMin + parameters.PanelGap) / xStep);
            var yCount = (int)Math.Floor((yMax - yMin + parameters.RowGap) / yStep);

            if (xCount <= 0 || yCount <= 0)
                return transforms;

            var usedWidth = xCount * parameters.PanelWidth + (xCount - 1) * parameters.PanelGap;
            var usedLength = yCount * parameters.PanelLength + (yCount - 1) * parameters.RowGap;
            var startX = xMin + ((xMax - xMin - usedWidth) * 0.5) + parameters.PanelWidth * 0.5;
            var startY = yMin + ((yMax - yMin - usedLength) * 0.5) + parameters.PanelLength * 0.5;

            for (var ix = 0; ix < xCount; ix++)
            {
                for (var iy = 0; iy < yCount; iy++)
                {
                    var cx = startX + ix * xStep;
                    var cy = startY + iy * yStep;

                    if (!PanelFits(polygon, cx, cy, parameters))
                        continue;

                    var worldOrigin = roof.Plane.Origin + xAxis * cx + yAxis * cy;
                    var panelPlane = new Plane(worldOrigin, xAxis, yAxis);
                    transforms.Add(Transform.PlaneToPlane(Plane.WorldXY, panelPlane));
                }
            }

            return transforms;
        }

        private static void OrientTiltTowardSouth(Plane roofPlane, ref Vector3d xAxis, ref Vector3d yAxis)
        {
            var south = new Vector3d(0.0, -1.0, 0.0);
            var normal = roofPlane.ZAxis;
            normal.Unitize();

            south = south - normal * (south * normal);
            if (!south.Unitize())
                south = new Vector3d(0.0, -1.0, 0.0);

            var currentDownDirection = -yAxis;
            currentDownDirection.Unitize();

            var flippedDownDirection = yAxis;
            flippedDownDirection.Unitize();

            if ((flippedDownDirection * south) > (currentDownDirection * south))
            {
                xAxis = -xAxis;
                yAxis = -yAxis;
            }
        }

        private static GridCandidate ChooseBestGridCandidate(RoofFace roof, PVParameters parameters, double tolerance)
        {
            var rectangleAxes = MinimumAreaRectangleAxes(roof.Polygon, roof.Plane, tolerance);
            var candidates = new List<GridCandidate>();

            candidates.Add(EvaluateGrid(roof, rectangleAxes.Item1, rectangleAxes.Item2, parameters));
            candidates.Add(EvaluateGrid(roof, rectangleAxes.Item2, -rectangleAxes.Item1, parameters));

            GridCandidate best = null;
            foreach (var candidate in candidates)
            {
                if (best == null || candidate.Count > best.Count)
                    best = candidate;
            }

            return best;
        }

        private static GridCandidate EvaluateGrid(RoofFace roof, Vector3d xAxis, Vector3d yAxis, PVParameters parameters)
        {
            var polygon = ProjectPolygonToAxes(roof, xAxis, yAxis);
            var xValues = GetProjectedRange(polygon, true);
            var yValues = GetProjectedRange(polygon, false);

            var xMin = xValues.Item1 + parameters.ParapetMargin;
            var xMax = xValues.Item2 - parameters.ParapetMargin;
            var yMin = yValues.Item1 + parameters.ParapetMargin;
            var yMax = yValues.Item2 - parameters.ParapetMargin;
            var xStep = parameters.PanelWidth + parameters.PanelGap;
            var yStep = parameters.PanelLength + parameters.RowGap;

            var xCount = (int)Math.Floor((xMax - xMin + parameters.PanelGap) / xStep);
            var yCount = (int)Math.Floor((yMax - yMin + parameters.RowGap) / yStep);
            var count = Math.Max(0, xCount) * Math.Max(0, yCount);

            return new GridCandidate { XAxis = xAxis, YAxis = yAxis, Count = count };
        }

        private static Tuple<Vector3d, Vector3d> MinimumAreaRectangleAxes(List<Point2> polygon, Plane plane, double tolerance)
        {
            var hull = ConvexHull(polygon);
            if (hull.Count < 2)
                return Tuple.Create(plane.XAxis, plane.YAxis);

            var bestArea = double.MaxValue;
            var bestX = plane.XAxis;
            var bestY = plane.YAxis;

            for (var i = 0; i < hull.Count; i++)
            {
                var a = hull[i];
                var b = hull[(i + 1) % hull.Count];
                var edge = b - a;
                var length = Math.Sqrt(edge.X * edge.X + edge.Y * edge.Y);
                if (length <= tolerance)
                    continue;

                var ux = edge.X / length;
                var uy = edge.Y / length;
                var vx = -uy;
                var vy = ux;

                var minU = double.MaxValue;
                var maxU = double.MinValue;
                var minV = double.MaxValue;
                var maxV = double.MinValue;

                foreach (var p in hull)
                {
                    var u = p.X * ux + p.Y * uy;
                    var v = p.X * vx + p.Y * vy;
                    minU = Math.Min(minU, u);
                    maxU = Math.Max(maxU, u);
                    minV = Math.Min(minV, v);
                    maxV = Math.Max(maxV, v);
                }

                var area = (maxU - minU) * (maxV - minV);
                if (area < bestArea)
                {
                    bestArea = area;
                    bestX = plane.XAxis * ux + plane.YAxis * uy;
                    bestY = plane.XAxis * vx + plane.YAxis * vy;
                    bestX.Unitize();
                    bestY.Unitize();
                }
            }

            return Tuple.Create(bestX, bestY);
        }

        private static List<Point2> ProjectPolygonToAxes(RoofFace roof, Vector3d xAxis, Vector3d yAxis)
        {
            var projected = new List<Point2>();
            foreach (var p in roof.Polygon)
            {
                var world = roof.Plane.Origin + roof.Plane.XAxis * p.X + roof.Plane.YAxis * p.Y;
                var vector = world - roof.Plane.Origin;
                projected.Add(new Point2(vector * xAxis, vector * yAxis));
            }
            return projected;
        }

        private static Tuple<double, double> GetProjectedRange(List<Point2> polygon, bool useX)
        {
            var min = double.MaxValue;
            var max = double.MinValue;
            foreach (var point in polygon)
            {
                var value = useX ? point.X : point.Y;
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }
            return Tuple.Create(min, max);
        }

        private static bool PanelFits(List<Point2> polygon, double cx, double cy, PVParameters parameters)
        {
            if (parameters.FitMode == PVFitMode.Loose)
                return PointInPolygon(new Point2(cx, cy), polygon);

            var halfWidth = parameters.PanelWidth * 0.5;
            var halfLength = parameters.PanelLength * 0.5;
            return PointInPolygon(new Point2(cx - halfWidth, cy - halfLength), polygon)
                && PointInPolygon(new Point2(cx + halfWidth, cy - halfLength), polygon)
                && PointInPolygon(new Point2(cx + halfWidth, cy + halfLength), polygon)
                && PointInPolygon(new Point2(cx - halfWidth, cy + halfLength), polygon);
        }

        private static bool PointInPolygon(Point2 point, List<Point2> polygon)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];
                var intersects = ((pi.Y > point.Y) != (pj.Y > point.Y))
                    && (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / ((pj.Y - pi.Y) + 1e-12) + pi.X);
                if (intersects)
                    inside = !inside;
            }
            return inside;
        }

        private static List<Point2> ConvexHull(List<Point2> points)
        {
            var sorted = new List<Point2>(points);
            sorted.Sort((a, b) =>
            {
                var x = a.X.CompareTo(b.X);
                return x != 0 ? x : a.Y.CompareTo(b.Y);
            });

            if (sorted.Count <= 1)
                return sorted;

            var lower = new List<Point2>();
            foreach (var p in sorted)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 1] - lower[lower.Count - 2], p - lower[lower.Count - 1]) <= 0.0)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(p);
            }

            var upper = new List<Point2>();
            for (var i = sorted.Count - 1; i >= 0; i--)
            {
                var p = sorted[i];
                while (upper.Count >= 2 && Cross(upper[upper.Count - 1] - upper[upper.Count - 2], p - upper[upper.Count - 1]) <= 0.0)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(p);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower;
        }

        private static double Cross(Point2 a, Point2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        private static double Distance(Point2 a, Point2 b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
