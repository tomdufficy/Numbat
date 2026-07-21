using System.Collections.Generic;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatHandrail
{
    internal class HandrailPreviewLabel
    {
        public HandrailPreviewLabel(Point3d point, string text)
        {
            Point = point;
            Text = text;
        }

        public Point3d Point { get; }
        public string Text { get; }
    }

    internal class HandrailPreviewLine
    {
        public HandrailPreviewLine(Point3d start, Point3d end)
        {
            Start = start;
            End = end;
        }

        public Point3d Start { get; }
        public Point3d End { get; }
    }

    internal class HandrailGeometry
    {
        public List<Brep> TopRails { get; } = new List<Brep>();
        public List<Brep> BottomRails { get; } = new List<Brep>();
        public List<Brep> Infill { get; } = new List<Brep>();
        public List<Brep> PanelFrames { get; } = new List<Brep>();
        public List<Brep> PanelSheets { get; } = new List<Brep>();
        public List<Brep> EndPosts { get; } = new List<Brep>();
        public List<Brep> IntermediatePosts { get; } = new List<Brep>();
        public List<Brep> SupportFeet { get; } = new List<Brep>();
        public List<Brep> Tabs { get; } = new List<Brep>();
        public List<HandrailPreviewLabel> PreviewLabels { get; } = new List<HandrailPreviewLabel>();
        public List<HandrailPreviewLine> PreviewLines { get; } = new List<HandrailPreviewLine>();

        public int PanelBaysReduced { get; set; }
        public int PanelBaysOmitted { get; set; }
        public int SheetBaysReduced { get; set; }
        public int SheetBaysOmitted { get; set; }

        public void Append(HandrailGeometry other)
        {
            if (other == null)
                return;

            TopRails.AddRange(other.TopRails);
            BottomRails.AddRange(other.BottomRails);
            Infill.AddRange(other.Infill);
            PanelFrames.AddRange(other.PanelFrames);
            PanelSheets.AddRange(other.PanelSheets);
            EndPosts.AddRange(other.EndPosts);
            IntermediatePosts.AddRange(other.IntermediatePosts);
            SupportFeet.AddRange(other.SupportFeet);
            Tabs.AddRange(other.Tabs);
            PreviewLabels.AddRange(other.PreviewLabels);
            PreviewLines.AddRange(other.PreviewLines);

            PanelBaysReduced += other.PanelBaysReduced;
            PanelBaysOmitted += other.PanelBaysOmitted;
            SheetBaysReduced += other.SheetBaysReduced;
            SheetBaysOmitted += other.SheetBaysOmitted;
        }

        public List<Brep> AllBreps()
        {
            var all = new List<Brep>();

            all.AddRange(TopRails);
            all.AddRange(BottomRails);
            all.AddRange(Infill);
            all.AddRange(PanelFrames);
            all.AddRange(PanelSheets);
            all.AddRange(EndPosts);
            all.AddRange(IntermediatePosts);
            all.AddRange(SupportFeet);
            all.AddRange(Tabs);

            return all;
        }
    }
}
