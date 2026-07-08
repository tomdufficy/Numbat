using System.Collections.Generic;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatSpiralStair
{
    internal class SpiralStairPreviewLabel
    {
        public SpiralStairPreviewLabel(Point3d point, string text)
        {
            Point = point;
            Text = text;
        }

        public Point3d Point { get; }
        public string Text { get; }
    }

    internal class SpiralStairPreviewLine
    {
        public SpiralStairPreviewLine(Point3d start, Point3d end)
        {
            Start = start;
            End = end;
        }

        public Point3d Start { get; }
        public Point3d End { get; }
    }

    internal class SpiralStairGeometry
    {
        public List<GeometryBase> Treads { get; } = new List<GeometryBase>();
        public List<GeometryBase> Risers { get; } = new List<GeometryBase>();
        public List<GeometryBase> CentreColumn { get; } = new List<GeometryBase>();
        public List<GeometryBase> Handrail { get; } = new List<GeometryBase>();
        public List<GeometryBase> Balusters { get; } = new List<GeometryBase>();
        public List<GeometryBase> Skin { get; } = new List<GeometryBase>();
        public List<GeometryBase> Landings { get; } = new List<GeometryBase>();
        public List<SpiralStairPreviewLabel> PreviewLabels { get; } = new List<SpiralStairPreviewLabel>();
        public List<SpiralStairPreviewLine> PreviewLines { get; } = new List<SpiralStairPreviewLine>();

        public List<GeometryBase> AllGeometry()
        {
            var all = new List<GeometryBase>();
            all.AddRange(Treads);
            all.AddRange(Risers);
            all.AddRange(CentreColumn);
            all.AddRange(Handrail);
            all.AddRange(Balusters);
            all.AddRange(Skin);
            all.AddRange(Landings);
            return all;
        }
    }
}
