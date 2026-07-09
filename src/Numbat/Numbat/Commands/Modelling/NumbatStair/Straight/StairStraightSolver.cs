using System;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatStair.Straight
{
    internal static class StairStraightSolver
    {
        public static StairStraightSolution Solve(StairStraightParameters parameters)
        {
            var direction = parameters.Direction;
            direction.Z = 0.0;
            if (!direction.Unitize())
                direction = Vector3d.XAxis;

            var widthDirection = Vector3d.CrossProduct(Vector3d.ZAxis, direction);
            if (!widthDirection.Unitize())
                widthDirection = Vector3d.YAxis;

            var riserCount = Math.Max(1, (int)Math.Ceiling(parameters.FloorHeight / Math.Max(1.0, parameters.MaxRiserHeight)));
            var treadCount = riserCount;
            var actualRiser = parameters.FloorHeight / riserCount;
            var maxSteps = Math.Max(1, parameters.MaxStepsBeforeLanding);
            var landingCount = Math.Max(0, (treadCount - 1) / maxSteps);
            var landingDepth = Math.Max(0.0, parameters.LandingDepth);
            var totalRun = treadCount * Math.Max(1.0, parameters.TreadDepth) + landingCount * landingDepth;

            var solution = new StairStraightSolution
            {
                Parameters = parameters,
                RiserCount = riserCount,
                TreadCount = treadCount,
                LandingCount = landingCount,
                ActualRiserHeight = actualRiser,
                TotalRunLength = totalRun,
                Direction = direction,
                WidthDirection = widthDirection
            };

            if (parameters.Width < 600.0)
                solution.Warning = "Stair width is small.";

            return solution;
        }
    }
}
