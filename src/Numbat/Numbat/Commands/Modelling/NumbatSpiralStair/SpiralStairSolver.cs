using System;

namespace Numbat.Commands.Modelling.NumbatSpiralStair
{
    internal static class SpiralStairSolver
    {
        public static SpiralStairSolution Solve(SpiralStairParameters parameters)
        {
            var riserCount = Math.Max(2, (int)Math.Ceiling(parameters.FloorHeight / Math.Max(1.0, parameters.MaxRiserHeight)));
            var treadCount = riserCount;
            var actualRiser = parameters.FloorHeight / riserCount;

            var target = GetTargetEndAngle(parameters.EndDirection, parameters.Direction);
            var totalRotation = ChooseTotalRotation(parameters.Radius, treadCount, target);
            var directionSign = parameters.Direction == SpiralStairDirection.Clockwise ? -1.0 : 1.0;
            var signedRotation = totalRotation * directionSign;
            var stepAngle = totalRotation / treadCount;

            var solution = new SpiralStairSolution
            {
                Parameters = parameters,
                RiserCount = riserCount,
                TreadCount = treadCount,
                ActualRiserHeight = actualRiser,
                TotalRotationRadians = totalRotation,
                SignedTotalRotationRadians = signedRotation,
                StepAngleRadians = stepAngle,
                SignedStepAngleRadians = stepAngle * directionSign,
                OuterTreadDepth = parameters.Radius * stepAngle
            };

            if (parameters.Radius < 800.0)
                solution.Warning = "Small radius. Stair may look tight.";
            else if (solution.OuterTreadDepth < 220.0)
                solution.Warning = "Outer tread depth is small.";
            else if (solution.OuterTreadDepth > 450.0)
                solution.Warning = "Outer tread depth is large.";

            return solution;
        }

        private static double GetTargetEndAngle(SpiralStairEndDirection endDirection, SpiralStairDirection direction)
        {
            switch (endDirection)
            {
                case SpiralStairEndDirection.Right90:
                    return direction == SpiralStairDirection.Clockwise ? Math.PI * 0.5 : Math.PI * 1.5;
                case SpiralStairEndDirection.Left90:
                    return direction == SpiralStairDirection.Clockwise ? Math.PI * 1.5 : Math.PI * 0.5;
                case SpiralStairEndDirection.Opposite:
                    return Math.PI;
                case SpiralStairEndDirection.Same:
                default:
                    return 0.0;
            }
        }

        private static double ChooseTotalRotation(double radius, int treadCount, double targetEndAngle)
        {
            const double idealDepth = 300.0;
            const double minDepth = 230.0;
            const double maxDepth = 420.0;

            var bestRotation = Math.PI * 2.0;
            var bestScore = double.MaxValue;

            for (var turns = 0; turns <= 8; turns++)
            {
                var candidate = targetEndAngle + turns * Math.PI * 2.0;

                if (candidate < Math.PI * 1.5)
                    continue;

                var depth = radius * candidate / treadCount;
                var score = Math.Abs(depth - idealDepth);

                if (depth < minDepth)
                    score += (minDepth - depth) * 8.0;

                if (depth > maxDepth)
                    score += (depth - maxDepth) * 8.0;

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
