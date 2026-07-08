using System.Collections.Generic;
using Rhino.Display;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatSpiralStair
{
    internal class SpiralStairPreviewConduit : DisplayConduit
    {
        public List<GeometryBase> PreviewGeometry { get; set; } = new List<GeometryBase>();
        public List<SpiralStairPreviewLabel> PreviewLabels { get; set; } = new List<SpiralStairPreviewLabel>();
        public List<SpiralStairPreviewLine> PreviewLines { get; set; } = new List<SpiralStairPreviewLine>();

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            foreach (var geometry in PreviewGeometry)
                e.IncludeBoundingBox(geometry.GetBoundingBox(true));

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

            foreach (var geometry in PreviewGeometry)
            {
                if (geometry is Brep brep)
                {
                    e.Display.DrawBrepShaded(brep, material);
                    e.Display.DrawBrepWires(brep, previewColor, 1);
                }
                else if (geometry is Mesh mesh)
                {
                    e.Display.DrawMeshShaded(mesh, material);
                    e.Display.DrawMeshWires(mesh, previewColor);
                }
                else if (geometry is Curve curve)
                {
                    e.Display.DrawCurve(curve, previewColor, 2);
                }
            }

            foreach (var line in PreviewLines)
                e.Display.DrawLine(line.Start, line.End, previewColor, 2);

            foreach (var label in PreviewLabels)
                e.Display.DrawDot(label.Point, label.Text, previewColor, textColor);
        }
    }
}
