using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatStair.Spiral
{
    internal static class StairSpiralBake
    {
        public static void AddGeometryToDocument(RhinoDoc doc, StairSpiralGeometry geometry)
        {
            AddGeometryAsBlocks(doc, geometry.Treads, "Numbat::Spiral Stair::Treads", Color.FromArgb(218, 196, 166), "Numbat_Spiral_Tread");
            AddGeometry(doc, geometry.FrontLips, "Numbat::Spiral Stair::Front Lips", Color.FromArgb(232, 184, 158));
            AddGeometryAsBlocks(doc, geometry.RearLips, "Numbat::Spiral Stair::Rear Lips", Color.FromArgb(205, 175, 218), "Numbat_Spiral_RearLip");
            AddGeometry(doc, geometry.Risers, "Numbat::Spiral Stair::Risers", Color.FromArgb(188, 166, 207));
            AddGeometry(doc, geometry.CentreColumn, "Numbat::Spiral Stair::Centre Column", Color.FromArgb(155, 169, 181));
            AddGeometry(doc, geometry.Handrail, "Numbat::Spiral Stair::Handrail", Color.FromArgb(150, 203, 219));
            AddGeometryAsBlocks(doc, geometry.Balusters, "Numbat::Spiral Stair::Balusters", Color.FromArgb(188, 224, 226), "Numbat_Spiral_Baluster");
            AddGeometry(doc, geometry.Skin, "Numbat::Spiral Stair::Solid Skin", Color.FromArgb(166, 202, 168));
            AddGeometry(doc, geometry.Soffit, "Numbat::Spiral Stair::Soffit", Color.FromArgb(190, 214, 186));
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

            var layerIndex = EnsureLayer(doc, layerPath, color);
            var attributes = CreateLayerAttributes(layerIndex);

            var metadataGroups = new Dictionary<string, List<Tuple<GeometryBase, Transform>>>();
            var remaining = new List<GeometryBase>();

            foreach (var geometry in items)
            {
                if (TryGetBlockTransform(geometry, out var blockKey, out var transform))
                {
                    if (!metadataGroups.ContainsKey(blockKey))
                        metadataGroups[blockKey] = new List<Tuple<GeometryBase, Transform>>();
                    metadataGroups[blockKey].Add(Tuple.Create(geometry, transform));
                }
                else
                {
                    remaining.Add(geometry);
                }
            }

            foreach (var group in metadataGroups)
            {
                if (group.Value.Count == 1)
                {
                    AddSingleGeometry(doc, group.Value[0].Item1, attributes);
                    continue;
                }

                var exemplar = group.Value[0].Item1.Duplicate();
                var exemplarTransform = group.Value[0].Item2;
                if (!exemplarTransform.TryGetInverse(out var inverse))
                {
                    AddGeometry(doc, ExtractGeometry(group.Value), layerPath, color);
                    continue;
                }

                exemplar.Transform(inverse);
                var blockName = UniqueBlockName(doc, group.Key);
                var definitionIndex = AddBlockDefinition(doc, blockName, exemplar, attributes);

                if (definitionIndex < 0)
                {
                    AddGeometry(doc, ExtractGeometry(group.Value), layerPath, color);
                    continue;
                }

                foreach (var pair in group.Value)
                    doc.Objects.AddInstanceObject(definitionIndex, pair.Item2, attributes);
            }

            AddRemainingAsBoundingBoxBlocks(doc, remaining, layerPath, color, blockNamePrefix, attributes);
        }

        private static IEnumerable<GeometryBase> ExtractGeometry(IEnumerable<Tuple<GeometryBase, Transform>> items)
        {
            foreach (var item in items)
                yield return item.Item1;
        }

        private static void AddRemainingAsBoundingBoxBlocks(RhinoDoc doc, List<GeometryBase> items, string layerPath, Color color, string blockNamePrefix, ObjectAttributes attributes)
        {
            if (items.Count == 0)
                return;

            if (items.Count == 1)
            {
                AddSingleGeometry(doc, items[0], attributes);
                return;
            }

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
                var exemplarCentre = exemplar.GetBoundingBox(true).Center;
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

        private static bool TryGetBlockTransform(GeometryBase geometry, out string blockKey, out Transform transform)
        {
            blockKey = geometry.GetUserString("NumbatBlockKey");
            var value = geometry.GetUserString("NumbatBlockTransform");
            transform = Transform.Identity;

            if (string.IsNullOrWhiteSpace(blockKey) || string.IsNullOrWhiteSpace(value))
                return false;

            var parts = value.Split(',');
            if (parts.Length != 16)
                return false;

            var numbers = new double[16];
            for (var i = 0; i < parts.Length; i++)
            {
                if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i]))
                    return false;
            }

            transform.M00 = numbers[0]; transform.M01 = numbers[1]; transform.M02 = numbers[2]; transform.M03 = numbers[3];
            transform.M10 = numbers[4]; transform.M11 = numbers[5]; transform.M12 = numbers[6]; transform.M13 = numbers[7];
            transform.M20 = numbers[8]; transform.M21 = numbers[9]; transform.M22 = numbers[10]; transform.M23 = numbers[11];
            transform.M30 = numbers[12]; transform.M31 = numbers[13]; transform.M32 = numbers[14]; transform.M33 = numbers[15];
            return true;
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

        private static void AddSingleGeometry(RhinoDoc doc, GeometryBase geometry, ObjectAttributes attributes)
        {
            if (geometry is Brep brep)
                doc.Objects.AddBrep(brep, attributes);
            else if (geometry is Mesh mesh)
                doc.Objects.AddMesh(mesh, attributes);
            else if (geometry is Curve curve)
                doc.Objects.AddCurve(curve, attributes);
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
