using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatStair.Straight
{
    internal enum StairStraightMode
    {
        Monolithic = 0,
        Stringers = 1
    }

    internal class StairStraightParameters
    {
        public Point3d StartPoint { get; set; } = Point3d.Origin;
        public Vector3d Direction { get; set; } = Vector3d.XAxis;
        public double Width { get; set; } = 1100.0;
        public double FloorHeight { get; set; } = 3000.0;
        public double MaxRiserHeight { get; set; } = 180.0;
        public int MaxStepsBeforeLanding { get; set; } = 12;
        public StairStraightMode Mode { get; set; } = StairStraightMode.Monolithic;

        public double TreadDepth { get; set; } = 280.0;
        public double TreadThickness { get; set; } = 50.0;
        public double LandingDepth { get; set; } = 1100.0;
        public double StringerWidth { get; set; } = 80.0;
        public double StringerDepth { get; set; } = 220.0;
    }
}
