using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatStair.Straight
{
    internal enum StairStraightMode
    {
        OpenStair = 0,
        SolidStair = 1,
        Monolithic = 2
    }

    internal enum StairStraightLandingMode
    {
        Middle = 0,
        MaxRisers = 1
    }

    internal enum StairStraightSwitchbackSide
    {
        Right = 0,
        Left = 1
    }

    internal class StairStraightParameters
    {
        public Point3d StartPoint { get; set; } = Point3d.Origin;
        public Vector3d Direction { get; set; } = Vector3d.XAxis;
        public double Width { get; set; } = 1100.0;
        public double FloorHeight { get; set; } = 3000.0;
        public double MaxRiserHeight { get; set; } = 180.0;
        public int MaxStepsBeforeLanding { get; set; } = 12;
        public StairStraightMode Mode { get; set; } = StairStraightMode.OpenStair;
        public StairStraightLandingMode LandingMode { get; set; } = StairStraightLandingMode.Middle;
        public bool Switchback { get; set; } = false;
        public StairStraightSwitchbackSide SwitchbackSide { get; set; } = StairStraightSwitchbackSide.Right;

        public double TreadDepth { get; set; } = 280.0;
        public double TreadThickness { get; set; } = 30.0;
        public double Nosing { get; set; } = 15.0;
        public double LandingDepth { get; set; } = 1100.0;
        public double StairThickness { get; set; } = 300.0;

        public double StringerWidth { get; set; } = 50.0;
        public double StringerDepth { get; set; } = 250.0;
        public double StringerInset { get; set; } = 0.0;
        public bool StringersEnabled { get; set; } = true;
        public bool StringersOutward { get; set; } = true;
    }
}
