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
            var dir = solution.Direction;
            var widthDir = solution.WidthDirection;
            var halfWidth = Math.Max(1.0, p.Width) * 0.5;
            var treadDepth = Math.Max(1.0, p.TreadDepth);
            var treadThickness = Math.Max(1.0, p.TreadThickness);
            var landingDepth = Math.Max(0.0, p.LandingDepth);
            var maxSteps = Math.Max(1, p.MaxStepsBeforeLanding);

            var runPosition = 0.0;

            for (var i = 0; i < solution.TreadCount; i++)
            {
                var topZ = (i + 1) * solution.ActualRiserHeight;
                var treadStart = start + dir * runPosition + new Vector3d(0.0, 0.0, topZ - treadThickness);
                geometry.Treads.Add(MakeBox(treadStart, dir, widthDir, treadDepth, halfWidth, treadThickness));

                if (p.Mode == StairStraightMode.Monolithic)
                {
                    var baseStart = start + dir * runPosition;
                    geometry.MonolithicBase.Add(MakeBox(baseStart, dir, widthDir, treadDepth, halfWidth, topZ));
                }

                runPosition += treadDepth;

                var needsLanding = i < solution.TreadCount - 1 && (i + 1) % maxSteps == 0;
                if (needsLanding)
                {
                    var landingStart = start + dir * runPosition + new Vector3d(0.0, 0.0, topZ - treadThickness);
                    geometry.Landings.Add(MakeBox(landingStart, dir, widthDir, landingDepth, halfWidth, treadThickness));

                    if (p.Mode == StairStraightMode.Monolithic)
                    {
                        var baseStart = start + dir * runPosition;
                        geometry.MonolithicBase.Add(MakeBox(baseStart, dir, widthDir, landingDepth, halfWidth, topZ));
                    }

                    runPosition += landingDepth;
                }
            }

            if (p.Mode == StairStraightMode.Stringers)
            {
                AddStringers(geometry, solution, start, dir, widthDir, halfWidth);
            }

            AddPreviewNotes(geometry, solution, start, dir, widthDir, halfWidth);
            return geometry;
        }

        private static void AddStringers(StairStraightGeometry geometry, StairStraightSolution solution, Point3d start, Vector3d dir, Vector3d widthDir, double halfWidth)
        {
            var p = solution.Parameters;
            var stringerWidth = Math.Max(1.0, p.StringerWidth);
            var stringerDepth = Math.Max(1.0, p.StringerDepth);
            var length = Math.Max(1.0, solution.TotalRunLength);
            var z = Math.Max(0.0, solution.Parameters.FloorHeight * 0.5 - stringerDepth * 0.5);
            var leftCentre = start + widthDir * (halfWidth - stringerWidth * 0.5) + new Vector3d(0.0, 0.0, z);
            var rightCentre = start - widthDir * (halfWidth - stringerWidth * 0.5) + new Vector3d(0.0, 0.0, z);

            geometry.Stringers.Add(MakeCentredBox(leftCentre, dir, widthDir, length, stringerWidth * 0.5, stringerDepth));
            geometry.Stringers.Add(MakeCentredBox(rightCentre, dir, widthDir, length, stringerWidth * 0.5, stringerDepth));
        }

        private static void AddPreviewNotes(StairStraightGeometry geometry, StairStraightSolution solution, Point3d start, Vector3d dir, Vector3d widthDir, double halfWidth)
        {
            var end = start + dir * solution.TotalRunLength;
            geometry.PreviewLines.Add(new StairStraightPreviewLine(start, end));
            geometry.PreviewLines.Add(new StairStraightPreviewLine(start - widthDir * halfWidth, start + widthDir * halfWidth));
            geometry.PreviewLabels.Add(new StairStraightPreviewLabel(end + widthDir * (halfWidth + 250.0), $"Run {solution.TotalRunLength:0} mm"));
            geometry.PreviewLabels.Add(new StairStraightPreviewLabel(start + widthDir * (halfWidth + 250.0), $"{solution.RiserCount} risers @ {solution.ActualRiserHeight:0.0} mm"));
        }

        private static Brep MakeBox(Point3d lowerBackCentre, Vector3d dir, Vector3d widthDir, double length, double halfWidth, double height)
        {
            var x = new Interval(0.0, Math.Max(1.0, length));
            var y = new Interval(-Math.Max(1.0, halfWidth), Math.Max(1.0, halfWidth));
            var z = new Interval(0.0, Math.Max(1.0, height));
            var plane = new Plane(lowerBackCentre, dir, widthDir);
            return Brep.CreateFromBox(new Box(plane, x, y, z));
        }

        private static Brep MakeCentredBox(Point3d centre, Vector3d dir, Vector3d widthDir, double length, double halfWidth, double height)
        {
            var x = new Interval(0.0, Math.Max(1.0, length));
            var y = new Interval(-Math.Max(1.0, halfWidth), Math.Max(1.0, halfWidth));
            var z = new Interval(-Math.Max(1.0, height) * 0.5, Math.Max(1.0, height) * 0.5);
            var plane = new Plane(centre, dir, widthDir);
            return Brep.CreateFromBox(new Box(plane, x, y, z));
        }
    }
}
