using System.Collections.Generic;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatStair.Spiral
{
    internal class StairSpiralPreviewLabel
    {
        public StairSpiralPreviewLabel(Point3d point, string text)
        {
            Point = point;
            Text = text;
        }

        public Point3d Point { get; }
        public string Text { get; }
    }

    internal class StairSpiralPreviewLine
    {
        public StairSpiralPreviewLine(Point3d start, Point3d end)
        {
            Start = start;
            End = end;
        }

        public Point3d Start { get; }
        public Point3d End { get; }
    }

    internal class StairSpiralGeometry
    {
        public List<GeometryBase> Treads { get; } = new List<GeometryBase>();
        public List<GeometryBase> FrontLips { get; } = new List<GeometryBase>();
        public List<GeometryBase> RearLips { get; } = new List<GeometryBase>();
        public List<GeometryBase> Risers { get; } = new List<GeometryBase>();
        public List<GeometryBase> CentreColumn { get; } = new List<GeometryBase>();
        public List<GeometryBase> Handrail { get; } = new List<GeometryBase>();
        public List<GeometryBase> Balusters { get; } = new List<GeometryBase>();
        public List<GeometryBase> Skin { get; } = new List<GeometryBase>();
        public List<GeometryBase> Soffit { get; } = new List<GeometryBase>();
        public List<StairSpiralPreviewLabel> PreviewLabels { get; } = new List<StairSpiralPreviewLabel>();
        public List<StairSpiralPreviewLine> PreviewLines { get; } = new List<StairSpiralPreviewLine>();

        public List<GeometryBase> AllGeometry()
        {
            var all = new List<GeometryBase>();
            all.AddRange(Treads);
            all.AddRange(FrontLips);
            all.AddRange(RearLips);
            all.AddRange(Risers);
            all.AddRange(CentreColumn);
            all.AddRange(Handrail);
            all.AddRange(Balusters);
            all.AddRange(Skin);
            all.AddRange(Soffit);
            return all;
        }
    }
}
