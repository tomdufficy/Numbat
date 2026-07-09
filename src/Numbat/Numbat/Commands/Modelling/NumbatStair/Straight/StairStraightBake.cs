using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatStair.Straight
{
    internal static class StairStraightBake
    {
        public static void AddGeometryToDocument(RhinoDoc doc, StairStraightGeometry geometry)
        {
            AddGeometry(doc, geometry.Treads, "Numbat::Straight Stair::Treads", Color.FromArgb(218, 196, 166));
            AddGeometry(doc, geometry.Landings, "Numbat::Straight Stair::Landings", Color.FromArgb(232, 184, 158));
            AddGeometry(doc, geometry.MonolithicBase, "Numbat::Straight Stair::Monolithic Base", Color.FromArgb(188, 166, 207));
            AddGeometry(doc, geometry.Stringers, "Numbat::Straight Stair::Stringers", Color.FromArgb(155, 169, 181));
        }

        private static void AddGeometry(RhinoDoc doc, IEnumerable<GeometryBase> geometryItems, string layerPath, Color color)
        {
            var hasGeometry = false;
            foreach (var geometry in geometryItems)
            {
                if (geometry != null)
                {
                    hasGeometry = true;
                    break;
                }
            }

            if (!hasGeometry)
                return;

            var layerIndex = EnsureLayer(doc, layerPath, color);
            var attributes = new ObjectAttributes
            {
                LayerIndex = layerIndex,
                ColorSource = ObjectColorSource.ColorFromLayer,
                MaterialSource = ObjectMaterialSource.MaterialFromLayer
            };

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
            {
                doc.Layers[existing].Color = color;
                return existing;
            }

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
