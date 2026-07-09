using System.Collections.Generic;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatStair.Straight
{
    internal class StairStraightPreviewLabel
    {
        public StairStraightPreviewLabel(Point3d point, string text)
        {
            Point = point;
            Text = text;
        }

        public Point3d Point { get; }
        public string Text { get; }
    }

    internal class StairStraightPreviewLine
    {
        public StairStraightPreviewLine(Point3d start, Point3d end)
        {
            Start = start;
            End = end;
        }

        public Point3d Start { get; }
        public Point3d End { get; }
    }

    internal class StairStraightGeometry
    {
        public List<GeometryBase> Treads { get; } = new List<GeometryBase>();
        public List<GeometryBase> Landings { get; } = new List<GeometryBase>();
        public List<GeometryBase> MonolithicBase { get; } = new List<GeometryBase>();
        public List<GeometryBase> Stringers { get; } = new List<GeometryBase>();
        public List<StairStraightPreviewLabel> PreviewLabels { get; } = new List<StairStraightPreviewLabel>();
        public List<StairStraightPreviewLine> PreviewLines { get; } = new List<StairStraightPreviewLine>();

        public List<GeometryBase> AllGeometry()
        {
            var all = new List<GeometryBase>();
            all.AddRange(Treads);
            all.AddRange(Landings);
            all.AddRange(MonolithicBase);
            all.AddRange(Stringers);
            return all;
        }
    }
}
