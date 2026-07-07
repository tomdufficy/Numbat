using System.Collections.Generic;
using Rhino.Display;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatHandrail
{
    internal class HandrailPreviewConduit : DisplayConduit
    {
        public List<Brep> PreviewBreps { get; set; } = new List<Brep>();
        public List<HandrailPreviewLabel> PreviewLabels { get; set; } = new List<HandrailPreviewLabel>();
        public List<HandrailPreviewLine> PreviewLines { get; set; } = new List<HandrailPreviewLine>();

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            foreach (var brep in PreviewBreps)
                e.IncludeBoundingBox(brep.GetBoundingBox(true));

            foreach (var label in PreviewLabels)
                e.IncludeBoundingBox(new BoundingBox(label.Point, label.Point));

            foreach (var line in PreviewLines)
                e.IncludeBoundingBox(new BoundingBox(line.Start, line.End));
        }

        protected override void DrawForeground(DrawEventArgs e)
        {
            var previewColor = System.Drawing.Color.FromArgb(246, 217, 245);
            var textColor = System.Drawing.Color.Black;
            var material = new DisplayMaterial(previewColor, 0.35);

            foreach (var brep in PreviewBreps)
            {
                e.Display.DrawBrepShaded(brep, material);
                e.Display.DrawBrepWires(brep, previewColor, 1);
            }

            foreach (var line in PreviewLines)
                e.Display.DrawLine(line.Start, line.End, previewColor, 2);

            foreach (var label in PreviewLabels)
                e.Display.DrawDot(label.Point, label.Text, previewColor, textColor);
        }
    }
}
