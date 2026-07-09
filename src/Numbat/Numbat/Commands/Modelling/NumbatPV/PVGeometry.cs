using System.Collections.Generic;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatPV
{
    internal class PVGeometry
    {
        public readonly List<Transform> PanelTransforms = new List<Transform>();
        public int RoofCount { get; set; }
        public int SkippedCount { get; set; }
    }
}
