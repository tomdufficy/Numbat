using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatStair.Straight
{
    internal static class StairStraightBake
    {
        public static void AddGeometryToDocument(RhinoDoc doc, StairStraightGeometry geometry)
        {
            AddGeometryAsBlocks(doc, geometry.Treads, "Numbat::Straight Stair::Treads", Color.FromArgb(218, 196, 166), "Numbat_Straight_Tread");
            AddGeometryAsBlocks(doc, geometry.Landings, "Numbat::Straight Stair::Landings", Color.FromArgb(232, 184, 158), "Numbat_Straight_Landing");
            AddGeometry(doc, geometry.MonolithicBase, "Numbat::Straight Stair::Monolithic Base", Color.FromArgb(188, 166, 207));
            AddGeometryAsBlocks(doc, geometry.Stringers, "Numbat::Straight Stair::Steel Stringers", Color.FromArgb(155, 169, 181), "Numbat_Straight_Stringer");
        }

        private static void AddGeometryAsBlocks(RhinoDoc doc, IEnumerable<GeometryBase> geometryItems, string layerPath, Color color, string blockNamePrefix)
        {
            var items = new List<GeometryBase>();
            foreach (var geometry in geometryItems)
            {
                if (geometry != null)
                    items.Add(geometry);
            }

            if (items.Count == 0)
                return;

            if (items.Count == 1)
            {
                AddGeometry(doc, items, layerPath, color);
                return;
            }

            var layerIndex = EnsureLayer(doc, layerPath, color);
            var attributes = CreateLayerAttributes(layerIndex);

            var groups = new Dictionary<string, List<GeometryBase>>();
            foreach (var geometry in items)
            {
                var key = BoundingBoxSizeKey(geometry.GetBoundingBox(true));
                if (!groups.ContainsKey(key))
                    groups[key] = new List<GeometryBase>();
                groups[key].Add(geometry);
            }

            foreach (var group in groups)
            {
                if (group.Value.Count == 1)
                {
                    AddSingleGeometry(doc, group.Value[0], attributes);
                    continue;
                }

                var exemplar = group.Value[0];
                var exemplarBox = exemplar.GetBoundingBox(true);
                var exemplarCentre = exemplarBox.Center;
                var blockName = UniqueBlockName(doc, blockNamePrefix + "_" + group.Key.Replace('|', '_'));
                var definitionIndex = AddBlockDefinition(doc, blockName, exemplar.Duplicate(), attributes);

                if (definitionIndex < 0)
                {
                    AddGeometry(doc, group.Value, layerPath, color);
                    continue;
                }

                foreach (var geometry in group.Value)
                {
                    var targetCentre = geometry.GetBoundingBox(true).Center;
                    var transform = Transform.Translation(targetCentre - exemplarCentre);
                    doc.Objects.AddInstanceObject(definitionIndex, transform, attributes);
                }
            }
        }

        private static int AddBlockDefinition(RhinoDoc doc, string blockName, GeometryBase definitionGeometry, ObjectAttributes attributes)
        {
            var geometry = new List<GeometryBase> { definitionGeometry };
            var definitionAttributes = new List<ObjectAttributes> { DuplicateAttributes(attributes) };
            return doc.InstanceDefinitions.Add(blockName, "Repeated Numbat stair component", Point3d.Origin, geometry, definitionAttributes);
        }

        private static ObjectAttributes DuplicateAttributes(ObjectAttributes source)
        {
            return new ObjectAttributes
            {
                LayerIndex = source.LayerIndex,
                ColorSource = source.ColorSource,
                MaterialSource = source.MaterialSource
            };
        }

        private static ObjectAttributes CreateLayerAttributes(int layerIndex)
        {
            return new ObjectAttributes
            {
                LayerIndex = layerIndex,
                ColorSource = ObjectColorSource.ColorFromLayer,
                MaterialSource = ObjectMaterialSource.MaterialFromLayer
            };
        }

        private static string BoundingBoxSizeKey(BoundingBox box)
        {
            var x = Math.Round(box.Max.X - box.Min.X, 3).ToString("0.###", CultureInfo.InvariantCulture);
            var y = Math.Round(box.Max.Y - box.Min.Y, 3).ToString("0.###", CultureInfo.InvariantCulture);
            var z = Math.Round(box.Max.Z - box.Min.Z, 3).ToString("0.###", CultureInfo.InvariantCulture);
            return x + "|" + y + "|" + z;
        }

        private static string UniqueBlockName(RhinoDoc doc, string baseName)
        {
            var name = baseName;
            var index = 1;
            while (doc.InstanceDefinitions.Find(name) != null)
            {
                name = baseName + "_" + index.ToString(CultureInfo.InvariantCulture);
                index++;
            }
            return name;
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
            var attributes = CreateLayerAttributes(layerIndex);

            foreach (var geometry in geometryItems)
            {
                if (geometry == null)
                    continue;

                AddSingleGeometry(doc, geometry, attributes);
            }
        }

        private static void AddSingleGeometry(RhinoDoc doc, GeometryBase geometry, ObjectAttributes attributes)
        {
            if (geometry is Brep brep)
                doc.Objects.AddBrep(brep, attributes);
            else if (geometry is Mesh mesh)
                doc.Objects.AddMesh(mesh, attributes);
            else if (geometry is Curve curve)
                doc.Objects.AddCurve(curve, attributes);
        }

        private static int EnsureLayer(RhinoDoc doc, string fullPath, Color color)
        {
            var existing = doc.Layers.FindByFullPath(fullPath, -1);
            if (existing >= 0)
            {
                doc.Layers[existing].Color = color;
                return existing;
            }

            var parts = fullPath.Split(new[] { "::" }, StringSplitOptions.None);
            var parentId = Guid.Empty;
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

                if (parentId != Guid.Empty)
                    layer.ParentLayerId = parentId;

                currentIndex = doc.Layers.Add(layer);
                parentId = doc.Layers[currentIndex].Id;
            }

            return currentIndex;
        }
    }
}
