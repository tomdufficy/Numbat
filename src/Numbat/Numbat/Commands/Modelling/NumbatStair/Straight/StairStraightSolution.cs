using System.Collections.Generic;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatStair.Straight
{
    internal class StairStraightFlight
    {
        public int StepCount { get; set; }
        public int StartRiserIndex { get; set; }
    }

    internal class StairStraightSolution
    {
        public StairStraightParameters Parameters { get; set; }
        public int RiserCount { get; set; }
        public int TreadCount { get; set; }
        public int LandingCount { get; set; }
        public int FlightCount { get; set; }
        public double ActualRiserHeight { get; set; }
        public double TotalRunLength { get; set; }
        public Vector3d Direction { get; set; }
        public Vector3d WidthDirection { get; set; }
        public List<StairStraightFlight> Flights { get; } = new List<StairStraightFlight>();
        public string Warning { get; set; }
    }
}
