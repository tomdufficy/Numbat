using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatStair.Straight
{
    internal class StairStraightSolution
    {
        public StairStraightParameters Parameters { get; set; }
        public int RiserCount { get; set; }
        public int TreadCount { get; set; }
        public int LandingCount { get; set; }
        public double ActualRiserHeight { get; set; }
        public double TotalRunLength { get; set; }
        public Vector3d Direction { get; set; }
        public Vector3d WidthDirection { get; set; }
        public string Warning { get; set; }
    }
}
