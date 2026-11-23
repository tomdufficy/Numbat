using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatStair.Spiral
{
    internal enum StairSpiralMode
    {
        Open = 0,
        Closed = 1
    }

    internal enum StairSpiralEndDirection
    {
        Same = 0,
        Right90 = 1,
        Left90 = 2,
        Opposite = 3
    }


    internal enum StairSpiralDirection
    {
        Clockwise = 0,
        CounterClockwise = 1
    }

    internal class StairSpiralParameters
    {
        public Point3d BaseCenter { get; set; } = Point3d.Origin;
        public double StartAngleRadians { get; set; }
        public double Radius { get; set; } = 1200.0;
        public double FloorHeight { get; set; } = 3000.0;
        public double MaxRiserHeight { get; set; } = 180.0;
        public double ColumnDiameter { get; set; } = 120.0;
        public double HandrailHeight { get; set; } = 900.0;
        public double ClosedSkinThickness { get; set; } = 0.0;
        public bool SplitClosedSkin { get; set; }
        public double HandrailDiameter { get; set; } = 35.0;
        public double BalusterDiameter { get; set; } = 16.0;
        public double SoffitThickness { get; set; } = 5.0;
        public StairSpiralEndDirection EndDirection { get; set; } = StairSpiralEndDirection.Same;
        public StairSpiralDirection Direction { get; set; } = StairSpiralDirection.Clockwise;
        public StairSpiralMode Mode { get; set; } = StairSpiralMode.Open;

        public double ColumnRadius => System.Math.Max(20.0, ColumnDiameter * 0.5);
        public double InnerRadius => System.Math.Max(20.0, ColumnRadius - 5.0);
        public double TreadThickness => 5.0;
        public double FoldDepth => 25.0;
        public double HandrailRadius => System.Math.Max(1.0, HandrailDiameter * 0.5);
        public double BalusterRadius => System.Math.Max(1.0, BalusterDiameter * 0.5);
        public double SolidGuardHeight => 1100.0;
        public double BalusterInsetFromOuterEdge => 35.0;
    }
}
