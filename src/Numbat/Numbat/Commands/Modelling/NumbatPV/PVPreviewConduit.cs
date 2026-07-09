using System.Collections.Generic;
using System.Drawing;
using Rhino.Display;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatPV
{
    internal class PVPreviewConduit : DisplayConduit
    {
        public List<GeometryBase> PreviewGeometry { get; set; } = new List<GeometryBase>();

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            foreach (var geometry in PreviewGeometry)
                e.IncludeBoundingBox(geometry.GetBoundingBox(true));
        }

        protected override void DrawForeground(DrawEventArgs e)
        {
            var previewColor = Color.FromArgb(246, 217, 245);
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
            }
        }
    }
}
