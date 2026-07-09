using System;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatStair.Straight
{
    internal static class StairStraightBuilder
    {
        public static StairStraightGeometry Build(StairStraightSolution solution, double tolerance)
        {
            var geometry = new StairStraightGeometry();
            var p = solution.Parameters;
            var start = p.StartPoint;
            var initialDir = solution.Direction;
            var halfWidth = Math.Max(1.0, p.Width) * 0.5;
            var treadDepth = Math.Max(1.0, p.TreadDepth);
            var treadThickness = Math.Max(1.0, p.TreadThickness);
            var nosing = Math.Max(0.0, p.Nosing);
            var landingDepth = Math.Max(0.0, p.LandingDepth);
            var switchbackSide = p.SwitchbackSide == StairStraightSwitchbackSide.Left ? -1.0 : 1.0;

            var flightStart = start;
            var flightDir = initialDir;

            for (var flightIndex = 0; flightIndex < solution.Flights.Count; flightIndex++)
            {
                var flight = solution.Flights[flightIndex];
                var widthDir = WidthDirectionFor(flightDir);
                var runPosition = 0.0;

                for (var i = 0; i < flight.StepCount; i++)
                {
                    var riserIndex = flight.StartRiserIndex + i + 1;
                    var topZ = riserIndex * solution.ActualRiserHeight;
                    var treadStart = flightStart + flightDir * (runPosition - nosing) + new Vector3d(0.0, 0.0, topZ - treadThickness);
                    geometry.Treads.Add(MakeBox(treadStart, flightDir, widthDir, treadDepth + nosing, halfWidth, treadThickness));

                    if (p.Mode == StairStraightMode.Monolithic)
                    {
                        var baseHeight = Math.Max(1.0, topZ - treadThickness);
                        var baseStart = flightStart + flightDir * runPosition;
                        geometry.MonolithicBase.Add(MakeBox(baseStart, flightDir, widthDir, treadDepth, halfWidth, baseHeight));
                    }

                    runPosition += treadDepth;
                }

                if (p.Mode == StairStraightMode.OpenStair && p.StringersEnabled)
                    AddStringersForFlight(geometry, solution, flightStart, flightDir, widthDir, halfWidth, flight);

                if (p.Mode == StairStraightMode.SolidStair)
                    AddSolidBodyForFlight(geometry, solution, flightStart, flightDir, widthDir, halfWidth, flight);

                var isLastFlight = flightIndex == solution.Flights.Count - 1;
                if (!isLastFlight)
                {
                    var landingTopZ = (flight.StartRiserIndex + flight.StepCount) * solution.ActualRiserHeight;
                    var landingCentreOffset = p.Switchback ? widthDir * (switchbackSide * halfWidth) : new Vector3d(0.0, 0.0, 0.0);
                    var landingHalfWidth = p.Switchback ? Math.Max(1.0, p.Width) : halfWidth;
                    var landingStart = flightStart + flightDir * runPosition + landingCentreOffset + new Vector3d(0.0, 0.0, landingTopZ - treadThickness);

                    geometry.Landings.Add(MakeBox(landingStart, flightDir, widthDir, landingDepth, landingHalfWidth, treadThickness));

                    if (p.Mode == StairStraightMode.Monolithic)
                    {
                        var baseHeight = Math.Max(1.0, landingTopZ - treadThickness);
                        var baseStart = flightStart + flightDir * runPosition + landingCentreOffset;
                        geometry.MonolithicBase.Add(MakeBox(baseStart, flightDir, widthDir, landingDepth, landingHalfWidth, baseHeight));
                    }
                    else if (p.Mode == StairStraightMode.OpenStair && p.StringersEnabled)
                    {
                        AddStringersForLanding(geometry, solution, flightStart + flightDir * runPosition + landingCentreOffset, flightDir, widthDir, landingHalfWidth, landingTopZ, landingDepth);
                    }
                    else if (p.Mode == StairStraightMode.SolidStair)
                    {
                        AddSolidBodyForLanding(geometry, solution, flightStart + flightDir * runPosition + landingCentreOffset, flightDir, widthDir, landingHalfWidth, landingTopZ, landingDepth);
                    }

                    if (p.Switchback)
                    {
                        flightStart = flightStart + flightDir * runPosition + widthDir * (switchbackSide * Math.Max(1.0, p.Width));
                        flightDir = -flightDir;
                    }
                    else
                    {
                        flightStart = flightStart + flightDir * (runPosition + landingDepth);
                    }
                }
            }

            AddPreviewNotes(geometry, solution, start, initialDir, halfWidth);
            return geometry;
        }

        private static Vector3d WidthDirectionFor(Vector3d dir)
        {
            var widthDir = Vector3d.CrossProduct(Vector3d.ZAxis, dir);
            if (!widthDir.Unitize())
                widthDir = Vector3d.YAxis;
            return widthDir;
        }

        private static void AddSolidBodyForFlight(StairStraightGeometry geometry, StairStraightSolution solution, Point3d flightStart, Vector3d dir, Vector3d widthDir, double halfWidth, StairStraightFlight flight)
        {
            var p = solution.Parameters;
            var treadDepth = Math.Max(1.0, p.TreadDepth);
            var treadThickness = Math.Max(1.0, p.TreadThickness);
            var solidDepth = Math.Max(80.0, p.StringerDepth);
            var runLength = Math.Max(1.0, flight.StepCount * treadDepth);

            var topProfile = new System.Collections.Generic.List<Point2d>();
            topProfile.Add(new Point2d(0.0, Math.Max(1.0, (flight.StartRiserIndex + 1) * solution.ActualRiserHeight - treadThickness)));

            for (var i = 0; i < flight.StepCount; i++)
            {
                var riserIndex = flight.StartRiserIndex + i + 1;
                var undersideZ = Math.Max(1.0, riserIndex * solution.ActualRiserHeight - treadThickness);
                var stepStartX = i * treadDepth;
                var stepEndX = (i + 1) * treadDepth;

                AddProfilePoint(topProfile, stepStartX, undersideZ);
                AddProfilePoint(topProfile, stepEndX, undersideZ);

                if (i < flight.StepCount - 1)
                {
                    var nextUndersideZ = Math.Max(1.0, (riserIndex + 1) * solution.ActualRiserHeight - treadThickness);
                    AddProfilePoint(topProfile, stepEndX, nextUndersideZ);
                }
            }

            var bottomStartZ = Math.Max(0.0, topProfile[0].Y - solidDepth);
            var bottomEndZ = Math.Max(0.0, topProfile[topProfile.Count - 1].Y - solidDepth);

            geometry.MonolithicBase.Add(MakeProfilePrism(flightStart, dir, widthDir, -halfWidth, halfWidth, topProfile, bottomStartZ, bottomEndZ));
        }

        private static void AddSolidBodyForLanding(StairStraightGeometry geometry, StairStraightSolution solution, Point3d landingStart, Vector3d dir, Vector3d widthDir, double landingHalfWidth, double landingTopZ, double landingDepth)
        {
            var p = solution.Parameters;
            var treadThickness = Math.Max(1.0, p.TreadThickness);
            var solidDepth = Math.Max(80.0, p.StringerDepth);
            var topZ = Math.Max(1.0, landingTopZ - treadThickness);
            var bottomZ = Math.Max(0.0, topZ - solidDepth);

            var topProfile = new System.Collections.Generic.List<Point2d>
            {
                new Point2d(0.0, topZ),
                new Point2d(Math.Max(1.0, landingDepth), topZ)
            };

            geometry.MonolithicBase.Add(MakeProfilePrism(landingStart, dir, widthDir, -landingHalfWidth, landingHalfWidth, topProfile, bottomZ, bottomZ));
        }

        private static void AddStringersForFlight(StairStraightGeometry geometry, StairStraightSolution solution, Point3d flightStart, Vector3d dir, Vector3d widthDir, double halfWidth, StairStraightFlight flight)
        {
            var p = solution.Parameters;
            var treadDepth = Math.Max(1.0, p.TreadDepth);
            var treadThickness = Math.Max(1.0, p.TreadThickness);
            var stringerWidth = Math.Max(1.0, p.StringerWidth);
            var stringerDepth = Math.Max(1.0, p.StringerDepth);
            var runLength = Math.Max(1.0, flight.StepCount * treadDepth);

            // Clean continuous rake line, with vertical start/end cuts.
            // The top line is aligned with the front upper corner rhythm of the treads.
            var topStartZ = (flight.StartRiserIndex + 1) * solution.ActualRiserHeight;
            var topSlope = solution.ActualRiserHeight / treadDepth;
            var topEndZ = topStartZ + (topSlope * runLength);
            topStartZ = Math.Max(topStartZ, flight.StartRiserIndex * solution.ActualRiserHeight + treadThickness);
            topEndZ = Math.Max(topEndZ, topStartZ + 1.0);

            geometry.Stringers.Add(MakeSlopingSidePlate(flightStart, dir, widthDir, runLength, halfWidth, stringerWidth, stringerDepth, topStartZ, topEndZ, 1.0, p.StringersOutward));
            geometry.Stringers.Add(MakeSlopingSidePlate(flightStart, dir, widthDir, runLength, halfWidth, stringerWidth, stringerDepth, topStartZ, topEndZ, -1.0, p.StringersOutward));
        }

        private static void AddStringersForLanding(StairStraightGeometry geometry, StairStraightSolution solution, Point3d landingStart, Vector3d dir, Vector3d widthDir, double landingHalfWidth, double landingTopZ, double landingDepth)
        {
            var p = solution.Parameters;
            var stringerWidth = Math.Max(1.0, p.StringerWidth);
            var stringerDepth = Math.Max(1.0, p.StringerDepth);
            var topZ = landingTopZ + solution.ActualRiserHeight;

            geometry.Stringers.Add(MakeLevelSidePlate(landingStart, dir, widthDir, landingDepth, landingHalfWidth, stringerWidth, stringerDepth, topZ, 1.0, p.StringersOutward));
            geometry.Stringers.Add(MakeLevelSidePlate(landingStart, dir, widthDir, landingDepth, landingHalfWidth, stringerWidth, stringerDepth, topZ, -1.0, p.StringersOutward));
        }

        private static void AddPreviewNotes(StairStraightGeometry geometry, StairStraightSolution solution, Point3d start, Vector3d dir, double halfWidth)
        {
            var widthDir = WidthDirectionFor(dir);
            geometry.PreviewLines.Add(new StairStraightPreviewLine(start - widthDir * halfWidth, start + widthDir * halfWidth));
            geometry.PreviewLabels.Add(new StairStraightPreviewLabel(start + widthDir * (halfWidth + 250.0), $"{solution.RiserCount} risers @ {solution.ActualRiserHeight:0.0} mm"));
            geometry.PreviewLabels.Add(new StairStraightPreviewLabel(start + widthDir * (halfWidth + 250.0) + new Vector3d(0.0, 0.0, 350.0), $"Flights {solution.FlightCount}, landings {solution.LandingCount}"));
        }

        private static void AddProfilePoint(System.Collections.Generic.List<Point2d> points, double x, double z)
        {
            if (points.Count > 0)
            {
                var last = points[points.Count - 1];
                if (Math.Abs(last.X - x) < 0.001 && Math.Abs(last.Y - z) < 0.001)
                    return;
            }

            points.Add(new Point2d(x, z));
        }

        private static Mesh MakeProfilePrism(Point3d basePoint, Vector3d dir, Vector3d widthDir, double yA, double yB, System.Collections.Generic.List<Point2d> topProfile, double bottomStartZ, double bottomEndZ)
        {
            var mesh = new Mesh();
            if (topProfile == null || topProfile.Count < 2)
                return mesh;

            var length = Math.Max(1.0, topProfile[topProfile.Count - 1].X - topProfile[0].X);

            double BottomZ(double x)
            {
                var t = length <= 0.001 ? 0.0 : (x - topProfile[0].X) / length;
                return bottomStartZ + (bottomEndZ - bottomStartZ) * t;
            }

            Point3d Local(double x, double y, double z)
            {
                return basePoint + dir * x + widthDir * y + new Vector3d(0.0, 0.0, z);
            }

            var section = new System.Collections.Generic.List<Point2d>();
            foreach (var point in topProfile)
                section.Add(point);

            for (var i = topProfile.Count - 1; i >= 0; i--)
            {
                var point = topProfile[i];
                AddProfilePoint(section, point.X, BottomZ(point.X));
            }

            var n = section.Count;
            for (var i = 0; i < n; i++)
                mesh.Vertices.Add(Local(section[i].X, yA, section[i].Y));

            for (var i = 0; i < n; i++)
                mesh.Vertices.Add(Local(section[i].X, yB, section[i].Y));

            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                mesh.Faces.AddFace(i, j, j + n, i + n);
            }

            AddCapFace(mesh, 0, n, false);
            AddCapFace(mesh, n, n, true);

            mesh.Normals.ComputeNormals();
            mesh.Compact();
            return mesh;
        }

        private static void AddCapFace(Mesh mesh, int offset, int count, bool reverse)
        {
            if (count < 3)
                return;

            for (var i = 1; i < count - 1; i++)
            {
                if (reverse)
                    mesh.Faces.AddFace(offset, offset + i + 1, offset + i);
                else
                    mesh.Faces.AddFace(offset, offset + i, offset + i + 1);
            }
        }

        private static Brep MakeBox(Point3d lowerBackCentre, Vector3d dir, Vector3d widthDir, double length, double halfWidth, double height)
        {
            var x = new Interval(0.0, Math.Max(1.0, length));
            var y = new Interval(-Math.Max(1.0, halfWidth), Math.Max(1.0, halfWidth));
            var z = new Interval(0.0, Math.Max(1.0, height));
            var plane = new Plane(lowerBackCentre, dir, widthDir);
            return Brep.CreateFromBox(new Box(plane, x, y, z));
        }

        private static Brep MakeSlopingSidePlate(Point3d basePoint, Vector3d dir, Vector3d widthDir, double length, double halfWidth, double plateWidth, double plateDepth, double topStartZ, double topEndZ, double side, bool outward)
        {
            GetStringerSideOffsets(halfWidth, plateWidth, side, outward, out var yA, out var yB);
            return MakeSidePlate(basePoint, dir, widthDir, length, yA, yB, topStartZ, topEndZ, plateDepth);
        }

        private static Brep MakeLevelSidePlate(Point3d basePoint, Vector3d dir, Vector3d widthDir, double length, double halfWidth, double plateWidth, double plateDepth, double topZ, double side, bool outward)
        {
            GetStringerSideOffsets(halfWidth, plateWidth, side, outward, out var yA, out var yB);
            return MakeSidePlate(basePoint, dir, widthDir, length, yA, yB, topZ, topZ, plateDepth);
        }

        private static void GetStringerSideOffsets(double halfWidth, double plateWidth, double side, bool outward, out double yA, out double yB)
        {
            // Centre the stringer on the tread edge line so changing stringer width
            // widens equally to both sides of that edge.
            var centre = side * halfWidth;
            var halfPlateWidth = Math.Max(1.0, plateWidth) * 0.5;
            yA = centre - halfPlateWidth;
            yB = centre + halfPlateWidth;
        }

        private static Brep MakeSidePlate(Point3d basePoint, Vector3d dir, Vector3d widthDir, double length, double yA, double yB, double topStartZ, double topEndZ, double depth)
        {
            var x0 = 0.0;
            var x1 = Math.Max(1.0, length);
            var bottomStartZ = topStartZ - Math.Max(1.0, depth);
            var bottomEndZ = topEndZ - Math.Max(1.0, depth);

            Point3d Local(double x, double y, double z)
            {
                return basePoint + dir * x + widthDir * y + new Vector3d(0.0, 0.0, z);
            }

            var corners = new[]
            {
                Local(x0, yA, bottomStartZ),
                Local(x1, yA, bottomEndZ),
                Local(x1, yB, bottomEndZ),
                Local(x0, yB, bottomStartZ),
                Local(x0, yA, topStartZ),
                Local(x1, yA, topEndZ),
                Local(x1, yB, topEndZ),
                Local(x0, yB, topStartZ)
            };

            return Brep.CreateFromBox(corners);
        }
    }
}
