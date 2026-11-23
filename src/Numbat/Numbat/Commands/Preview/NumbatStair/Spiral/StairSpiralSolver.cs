using System;

namespace Numbat.Commands.Modelling.NumbatStair.Spiral
{
    internal static class StairSpiralSolver
    {
        public static StairSpiralSolution Solve(StairSpiralParameters parameters)
        {
            var riserCount = Math.Max(2, (int)Math.Ceiling(parameters.FloorHeight / Math.Max(1.0, parameters.MaxRiserHeight)));
            var treadCount = riserCount;
            var actualRiser = parameters.FloorHeight / riserCount;
            var target = GetTargetEndAngle(parameters.EndDirection, parameters.Direction);
            var totalRotation = ChooseTotalRotation(parameters.Radius, treadCount, target);
            var directionSign = parameters.Direction == StairSpiralDirection.Clockwise ? -1.0 : 1.0;
            var stepAngle = totalRotation / treadCount;

            var solution = new StairSpiralSolution
            {
                Parameters = parameters,
                RiserCount = riserCount,
                TreadCount = treadCount,
                ActualRiserHeight = actualRiser,
                TotalRotationRadians = totalRotation,
                SignedTotalRotationRadians = totalRotation * directionSign,
                StepAngleRadians = stepAngle,
                SignedStepAngleRadians = stepAngle * directionSign,
                OuterTreadDepth = parameters.Radius * stepAngle
            };

            if (parameters.Radius < 800.0)
                solution.Warning = "Small radius. Stair may look tight.";
            else if (solution.OuterTreadDepth < 220.0)
                solution.Warning = "Treads may look tight at this radius.";

            return solution;
        }

        private static double GetTargetEndAngle(StairSpiralEndDirection endDirection, StairSpiralDirection direction)
        {
            switch (endDirection)
            {
                case StairSpiralEndDirection.Right90:
                    return direction == StairSpiralDirection.Clockwise ? Math.PI * 0.5 : Math.PI * 1.5;
                case StairSpiralEndDirection.Left90:
                    return direction == StairSpiralDirection.Clockwise ? Math.PI * 1.5 : Math.PI * 0.5;
                case StairSpiralEndDirection.Opposite:
                    return Math.PI;
                case StairSpiralEndDirection.Same:
                default:
                    return 0.0;
            }
        }

        private static double ChooseTotalRotation(double radius, int treadCount, double targetEndAngle)
        {
            const double idealOuterDepth = 300.0;
            const double minOuterDepth = 230.0;
            var bestRotation = Math.PI * 2.0;
            var bestScore = double.MaxValue;

            for (var turns = 0; turns <= 8; turns++)
            {
                var candidate = targetEndAngle + turns * Math.PI * 2.0;
                if (candidate < Math.PI * 1.5)
                    continue;

                var depth = radius * candidate / treadCount;
                var score = Math.Abs(depth - idealOuterDepth);
                if (depth < minOuterDepth)
                    score += (minOuterDepth - depth) * 8.0;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestRotation = candidate;
                }
            }

            return bestRotation;
        }
    }
}
