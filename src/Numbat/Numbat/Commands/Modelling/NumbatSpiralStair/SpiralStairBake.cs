using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatSpiralStair
{
    internal static class SpiralStairBake
    {
        public static void AddGeometryToDocument(RhinoDoc doc, SpiralStairGeometry geometry)
        {
            AddGeometry(doc, geometry.Treads, "Numbat::Spiral Stair::Treads", Color.FromArgb(205, 190, 160));
            AddGeometry(doc, geometry.Risers, "Numbat::Spiral Stair::Risers", Color.FromArgb(190, 175, 150));
            AddGeometry(doc, geometry.CentreColumn, "Numbat::Spiral Stair::Centre Column", Color.FromArgb(150, 160, 170));
            AddGeometry(doc, geometry.Handrail, "Numbat::Spiral Stair::Handrail", Color.FromArgb(160, 205, 220));
            AddGeometry(doc, geometry.Balusters, "Numbat::Spiral Stair::Balusters", Color.FromArgb(180, 220, 230));
            AddGeometry(doc, geometry.Skin, "Numbat::Spiral Stair::Solid Skin", Color.FromArgb(160, 190, 160));
            AddGeometry(doc, geometry.Landings, "Numbat::Spiral Stair::Landings", Color.FromArgb(220, 205, 170));
        }

        private static void AddGeometry(RhinoDoc doc, IEnumerable<GeometryBase> geometryItems, string layerPath, Color color)
        {
            var layerIndex = EnsureLayer(doc, layerPath, color);
            var attributes = new ObjectAttributes { LayerIndex = layerIndex };

            foreach (var geometry in geometryItems)
            {
                if (geometry == null)
                    continue;

                if (geometry is Brep brep)
                    doc.Objects.AddBrep(brep, attributes);
                else if (geometry is Mesh mesh)
                    doc.Objects.AddMesh(mesh, attributes);
                else if (geometry is Curve curve)
                    doc.Objects.AddCurve(curve, attributes);
            }
        }

        private static int EnsureLayer(RhinoDoc doc, string fullPath, Color color)
        {
            var existing = doc.Layers.FindByFullPath(fullPath, -1);
            if (existing >= 0)
                return existing;

            var parts = fullPath.Split(new[] { "::" }, System.StringSplitOptions.None);
            var parentId = System.Guid.Empty;
            var currentPath = string.Empty;
            var currentIndex = -1;

            foreach (var part in parts)
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? part : currentPath + "::" + part;
                currentIndex = doc.Layers.FindByFullPath(currentPath, -1);

                if (currentIndex >= 0)
                {
                    parentId = doc.Layers[currentIndex].Id;
                    continue;
                }

                var layer = new Layer
                {
                    Name = part,
                    Color = color
                };

                if (parentId != System.Guid.Empty)
                    layer.ParentLayerId = parentId;

                currentIndex = doc.Layers.Add(layer);
                parentId = doc.Layers[currentIndex].Id;
            }

            return currentIndex;
        }
    }
}
