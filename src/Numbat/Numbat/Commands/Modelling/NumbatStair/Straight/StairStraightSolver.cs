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
            var treadDepth = Math.Max(1.0, parameters.TreadDepth);
            var landingDepth = Math.Max(0.0, parameters.LandingDepth);

            var solution = new StairStraightSolution
            {
                Parameters = parameters,
                RiserCount = riserCount,
                TreadCount = treadCount,
                ActualRiserHeight = actualRiser,
                Direction = direction,
                WidthDirection = widthDirection
            };

            AddFlights(solution, parameters, treadCount);
            solution.FlightCount = solution.Flights.Count;
            solution.LandingCount = Math.Max(0, solution.Flights.Count - 1);
            solution.TotalRunLength = treadCount * treadDepth + solution.LandingCount * landingDepth;

            if (parameters.Width < 600.0)
                solution.Warning = "Stair width is small.";

            return solution;
        }

        private static void AddFlights(StairStraightSolution solution, StairStraightParameters parameters, int treadCount)
        {
            if (treadCount <= 0)
                return;

            if (parameters.LandingMode == StairStraightLandingMode.Middle)
            {
                if (treadCount <= 2)
                {
                    solution.Flights.Add(new StairStraightFlight { StepCount = treadCount, StartRiserIndex = 0 });
                    return;
                }

                var first = (int)Math.Ceiling(treadCount / 2.0);
                var second = treadCount - first;

                solution.Flights.Add(new StairStraightFlight { StepCount = first, StartRiserIndex = 0 });
                if (second > 0)
                    solution.Flights.Add(new StairStraightFlight { StepCount = second, StartRiserIndex = first });

                return;
            }

            var maxSteps = Math.Max(1, parameters.MaxStepsBeforeLanding);
            var used = 0;
            while (used < treadCount)
            {
                var count = Math.Min(maxSteps, treadCount - used);
                solution.Flights.Add(new StairStraightFlight { StepCount = count, StartRiserIndex = used });
                used += count;
            }
        }
    }
}
