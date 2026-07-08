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

    internal enum SpiralStairTopLanding
    {
        None = 0,
        Pie = 1,
        Rectangular = 2
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
        public SpiralStairEndDirection EndDirection { get; set; } = SpiralStairEndDirection.Same;
        public SpiralStairDirection Direction { get; set; } = SpiralStairDirection.Clockwise;
        public SpiralStairMode Mode { get; set; } = SpiralStairMode.Open;
        public SpiralStairTopLanding TopLanding { get; set; } = SpiralStairTopLanding.None;

        public double InnerRadius
        {
            get
            {
                var value = Radius * 0.28;
                if (value < 225.0)
                    value = 225.0;

                if (value > Radius - 450.0)
                    value = Radius - 450.0;

                if (value < 125.0)
                    value = 125.0;

                return value;
            }
        }

        public double ColumnRadius => 75.0;
        public double TreadThickness => 50.0;
        public double TreadFrontLip => 35.0;
        public double TreadBackLip => 35.0;
        public double HandrailHeight => 1100.0;
        public double HandrailRadius => 25.0;
        public double BalusterRadius => 12.0;
        public double SolidGuardHeight => 1100.0;
    }
}
