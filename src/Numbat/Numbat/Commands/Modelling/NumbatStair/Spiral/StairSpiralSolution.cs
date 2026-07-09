using System;

namespace Numbat.Commands.Modelling.NumbatStair.Spiral
{
    internal class StairSpiralSolution
    {
        public StairSpiralParameters Parameters { get; set; }
        public int RiserCount { get; set; }
        public int TreadCount { get; set; }
        public double ActualRiserHeight { get; set; }
        public double TotalRotationRadians { get; set; }
        public double SignedTotalRotationRadians { get; set; }
        public double StepAngleRadians { get; set; }
        public double SignedStepAngleRadians { get; set; }
        public double OuterTreadDepth { get; set; }
        public string Warning { get; set; } = string.Empty;

        public double TotalRotationDegrees => Rhino.RhinoMath.ToDegrees(TotalRotationRadians);
        public double SignedTotalRotationDegrees => Rhino.RhinoMath.ToDegrees(SignedTotalRotationRadians);
        public double StepAngleDegrees => Rhino.RhinoMath.ToDegrees(StepAngleRadians);
        public double Turns => TotalRotationRadians / (Math.PI * 2.0);
    }
}
