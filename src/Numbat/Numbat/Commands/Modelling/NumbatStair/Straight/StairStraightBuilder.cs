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

                if (p.Mode == StairStraightMode.Stringers)
                    AddStringersForFlight(geometry, solution, flightStart, flightDir, widthDir, halfWidth, flight);

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
                    else
                    {
                        AddStringersForLanding(geometry, solution, flightStart + flightDir * runPosition + landingCentreOffset, flightDir, widthDir, landingHalfWidth, landingTopZ, landingDepth);
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

        private static void AddStringersForFlight(StairStraightGeometry geometry, StairStraightSolution solution, Point3d flightStart, Vector3d dir, Vector3d widthDir, double halfWidth, StairStraightFlight flight)
        {
            var p = solution.Parameters;
            var treadDepth = Math.Max(1.0, p.TreadDepth);
            var treadThickness = Math.Max(1.0, p.TreadThickness);
            var stringerWidth = Math.Max(1.0, p.StringerWidth);
            var stringerDepth = Math.Max(1.0, p.StringerDepth);
            var runLength = Math.Max(1.0, flight.StepCount * treadDepth);

            var topStartZ = (flight.StartRiserIndex + 1) * solution.ActualRiserHeight;
            var topEndZ = (flight.StartRiserIndex + flight.StepCount) * solution.ActualRiserHeight;
            topStartZ = Math.Max(topStartZ, flight.StartRiserIndex * solution.ActualRiserHeight + treadThickness);
            topEndZ = Math.Max(topEndZ, topStartZ + 1.0);

            geometry.Stringers.Add(MakeSlopingSidePlate(flightStart, dir, widthDir, runLength, halfWidth, stringerWidth, stringerDepth, topStartZ, topEndZ, 1.0));
            geometry.Stringers.Add(MakeSlopingSidePlate(flightStart, dir, widthDir, runLength, halfWidth, stringerWidth, stringerDepth, topStartZ, topEndZ, -1.0));
        }

        private static void AddStringersForLanding(StairStraightGeometry geometry, StairStraightSolution solution, Point3d landingStart, Vector3d dir, Vector3d widthDir, double landingHalfWidth, double landingTopZ, double landingDepth)
        {
            var p = solution.Parameters;
            var stringerWidth = Math.Max(1.0, p.StringerWidth);
            var stringerDepth = Math.Max(1.0, p.StringerDepth);
            var topZ = landingTopZ;

            geometry.Stringers.Add(MakeLevelSidePlate(landingStart, dir, widthDir, landingDepth, landingHalfWidth, stringerWidth, stringerDepth, topZ, 1.0));
            geometry.Stringers.Add(MakeLevelSidePlate(landingStart, dir, widthDir, landingDepth, landingHalfWidth, stringerWidth, stringerDepth, topZ, -1.0));
        }

        private static void AddPreviewNotes(StairStraightGeometry geometry, StairStraightSolution solution, Point3d start, Vector3d dir, double halfWidth)
        {
            var widthDir = WidthDirectionFor(dir);
            geometry.PreviewLines.Add(new StairStraightPreviewLine(start - widthDir * halfWidth, start + widthDir * halfWidth));
            geometry.PreviewLabels.Add(new StairStraightPreviewLabel(start + widthDir * (halfWidth + 250.0), $"{solution.RiserCount} risers @ {solution.ActualRiserHeight:0.0} mm"));
            geometry.PreviewLabels.Add(new StairStraightPreviewLabel(start + widthDir * (halfWidth + 250.0) + new Vector3d(0.0, 0.0, 350.0), $"Flights {solution.FlightCount}, landings {solution.LandingCount}"));
        }

        private static Brep MakeBox(Point3d lowerBackCentre, Vector3d dir, Vector3d widthDir, double length, double halfWidth, double height)
        {
            var x = new Interval(0.0, Math.Max(1.0, length));
            var y = new Interval(-Math.Max(1.0, halfWidth), Math.Max(1.0, halfWidth));
            var z = new Interval(0.0, Math.Max(1.0, height));
            var plane = new Plane(lowerBackCentre, dir, widthDir);
            return Brep.CreateFromBox(new Box(plane, x, y, z));
        }

        private static Brep MakeSlopingSidePlate(Point3d basePoint, Vector3d dir, Vector3d widthDir, double length, double halfWidth, double plateWidth, double plateDepth, double topStartZ, double topEndZ, double side)
        {
            var yOuter = side * halfWidth;
            var yInner = side * Math.Max(0.0, halfWidth - plateWidth);
            return MakeSidePlate(basePoint, dir, widthDir, length, yInner, yOuter, topStartZ, topEndZ, plateDepth);
        }

        private static Brep MakeLevelSidePlate(Point3d basePoint, Vector3d dir, Vector3d widthDir, double length, double halfWidth, double plateWidth, double plateDepth, double topZ, double side)
        {
            var yOuter = side * halfWidth;
            var yInner = side * Math.Max(0.0, halfWidth - plateWidth);
            return MakeSidePlate(basePoint, dir, widthDir, length, yInner, yOuter, topZ, topZ, plateDepth);
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
