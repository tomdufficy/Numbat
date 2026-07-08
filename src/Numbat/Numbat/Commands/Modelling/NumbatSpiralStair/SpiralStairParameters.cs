using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatSpiralStair
{
    internal enum SpiralStairMode
    {
        Open = 0,
        Closed = 1
    }

    internal enum SpiralStairEndDirection
    {
        Same = 0,
        Right90 = 1,
        Left90 = 2,
        Opposite = 3
    }


    internal enum SpiralStairDirection
    {
        Clockwise = 0,
        CounterClockwise = 1
    }

    internal class SpiralStairParameters
    {
        public Point3d BaseCenter { get; set; } = Point3d.Origin;
        public double StartAngleRadians { get; set; }
        public double Radius { get; set; } = 1200.0;
        public double FloorHeight { get; set; } = 3000.0;
        public double MaxRiserHeight { get; set; } = 180.0;
        public double ColumnDiameter { get; set; } = 150.0;
        public double HandrailHeight { get; set; } = 900.0;
        public double ClosedSkinThickness { get; set; } = 0.0;
        public bool SplitClosedSkin { get; set; }
        public SpiralStairEndDirection EndDirection { get; set; } = SpiralStairEndDirection.Same;
        public SpiralStairDirection Direction { get; set; } = SpiralStairDirection.Clockwise;
        public SpiralStairMode Mode { get; set; } = SpiralStairMode.Open;

        public double ColumnRadius => System.Math.Max(20.0, ColumnDiameter * 0.5);
        public double InnerRadius => System.Math.Max(20.0, ColumnRadius - 5.0);
        public double TreadThickness => 5.0;
        public double FoldDepth => 25.0;
        public double HandrailRadius => 17.5;
        public double BalusterRadius => 8.0;
        public double SolidGuardHeight => 1100.0;
        public double BalusterInsetFromOuterEdge => 35.0;
    }
}
