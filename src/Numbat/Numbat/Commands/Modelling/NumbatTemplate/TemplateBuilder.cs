using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Numbat.Commands.Modelling.NumbatTemplate
{
    internal static class TemplateBuilder
    {
        private const string TextLayerPath = "DATA::DATA_TEXT AND SYMBOLS";
        private const string FormatLayerPath = "DATA::DATA_FORMAT";

        public static Result Create(RhinoDoc doc, Point3d startPoint)
        {
            List<TemplateData.LayerSpec> layers = new List<TemplateData.LayerSpec>(TemplateData.EnumerateLayers());
            int[] layerMap = CreateLayers(doc, layers);

            double scale = RhinoMath.UnitScale(UnitSystem.Millimeters, doc.ModelUnitSystem);
            double cubeSize = 1000.0 * scale;
            double rowGap = 500.0 * scale;
            double rowSpacing = cubeSize + rowGap;
            double columnWidth = 14000.0 * scale;
            double indent = 750.0 * scale;
            double textGap = 500.0 * scale;
            double textHeight = 250.0 * scale;
            double padding = 500.0 * scale;
            int textLayerIndex = FindLayerIndexByFullPath(doc, TextLayerPath);
            int formatLayerIndex = FindLayerIndexByFullPath(doc, FormatLayerPath);

            if (textLayerIndex < 0 || formatLayerIndex < 0)
            {
                RhinoApp.WriteLine("nbTemplate could not find the required DATA text/format layers.");
                return Result.Failure;
            }

            BoundingBox totalBounds = BoundingBox.Empty;
            int cubeCount = 0;
            int textCount = 0;

            Dictionary<int, int> rootToColumn = new Dictionary<int, int>();
            Dictionary<int, int> rootToRowCount = new Dictionary<int, int>();
            int nextColumn = 0;

            for (int i = 0; i < layers.Count; i++)
            {
                int depth = GetDepth(layers, i);
                int root = GetRootIndex(layers, i);

                if (!rootToColumn.ContainsKey(root))
                {
                    rootToColumn[root] = nextColumn;
                    rootToRowCount[root] = 0;
                    nextColumn++;
                }

                int column = rootToColumn[root];
                int row = rootToRowCount[root];
                rootToRowCount[root] = row + 1;

                double x = startPoint.X + column * columnWidth - (column > 0 ? 9000.0 * scale : 0.0) + depth * indent;
                double y = startPoint.Y - row * rowSpacing;
                double z = startPoint.Z;

                Point3d min = new Point3d(x, y - cubeSize, z);
                Point3d max = new Point3d(x + cubeSize, y, z + cubeSize);
                Box box = new Box(new BoundingBox(min, max));

                ObjectAttributes cubeAttributes = new ObjectAttributes
                {
                    LayerIndex = layerMap[i]
                };

                Guid cubeId = doc.Objects.AddBrep(box.ToBrep(), cubeAttributes);
                if (cubeId != Guid.Empty)
                {
                    cubeCount++;
                    AddObjectBounds(doc, cubeId, ref totalBounds);
                }

                string fullLayerPath = GetFullPath(layers, i);
                TextEntity text = new TextEntity
                {
                    Plane = new Plane(new Point3d(x + cubeSize + textGap, y - cubeSize / 2.0, z), Vector3d.XAxis, Vector3d.YAxis),
                    PlainText = fullLayerPath,
                    TextHeight = textHeight,
                    Justification = TextJustification.MiddleLeft
                };

                ObjectAttributes textAttributes = new ObjectAttributes
                {
                    LayerIndex = textLayerIndex
                };

                Guid textId = doc.Objects.AddText(text, textAttributes);
                if (textId != Guid.Empty)
                {
                    textCount++;
                    AddObjectBounds(doc, textId, ref totalBounds);
                }
            }

            if (totalBounds.IsValid)
                AddFormatBox(doc, totalBounds, padding, formatLayerIndex);

            doc.Views.Redraw();
            RhinoApp.WriteLine("nbTemplate created {0} layer(s), {1} cube(s), and {2} text label(s).", layers.Count, cubeCount, textCount);
            return Result.Success;
        }

        private static int[] CreateLayers(RhinoDoc doc, List<TemplateData.LayerSpec> layers)
        {
            int[] layerMap = new int[layers.Count];

            for (int i = 0; i < layers.Count; i++)
            {
                TemplateData.LayerSpec spec = layers[i];
                string fullPath = GetFullPath(layers, i);

                Layer existing = FindLayerByFullPath(doc, fullPath);
                if (existing != null)
                {
                    existing.Color = Color.FromArgb(spec.Red, spec.Green, spec.Blue);
                    existing.IsVisible = true;
                    existing.IsLocked = false;
                    doc.Layers.Modify(existing, existing.Index, true);
                    layerMap[i] = existing.Index;
                    continue;
                }

                Layer layer = new Layer
                {
                    Name = spec.Name,
                    Color = Color.FromArgb(spec.Red, spec.Green, spec.Blue),
                    IsVisible = true,
                    IsLocked = false
                };

                if (spec.ParentIndex >= 0 && spec.ParentIndex < layerMap.Length)
                {
                    Layer parent = doc.Layers[layerMap[spec.ParentIndex]];
                    if (parent != null)
                        layer.ParentLayerId = parent.Id;
                }

                int index = doc.Layers.Add(layer);
                layerMap[i] = index;
            }

            return layerMap;
        }

        private static void AddFormatBox(RhinoDoc doc, BoundingBox bounds, double padding, int layerIndex)
        {
            double minX = bounds.Min.X - padding;
            double maxX = bounds.Max.X + padding;
            double minY = bounds.Min.Y - padding;
            double maxY = bounds.Max.Y + padding;
            double z = bounds.Min.Z;

            Polyline polyline = new Polyline
            {
                new Point3d(minX, maxY, z),
                new Point3d(maxX, maxY, z),
                new Point3d(maxX, minY, z),
                new Point3d(minX, minY, z),
                new Point3d(minX, maxY, z)
            };

            ObjectAttributes attributes = new ObjectAttributes
            {
                LayerIndex = layerIndex
            };

            doc.Objects.AddPolyline(polyline, attributes);
        }

        private static void AddObjectBounds(RhinoDoc doc, Guid id, ref BoundingBox totalBounds)
        {
            RhinoObject obj = doc.Objects.FindId(id);
            if (obj == null)
                return;

            BoundingBox bounds = obj.Geometry.GetBoundingBox(true);
            if (!bounds.IsValid)
                return;

            if (!totalBounds.IsValid)
                totalBounds = bounds;
            else
                totalBounds.Union(bounds);
        }

        private static int GetDepth(List<TemplateData.LayerSpec> layers, int index)
        {
            int depth = 0;
            int parent = layers[index].ParentIndex;

            while (parent >= 0 && parent < layers.Count)
            {
                depth++;
                parent = layers[parent].ParentIndex;
            }

            return depth;
        }

        private static int GetRootIndex(List<TemplateData.LayerSpec> layers, int index)
        {
            int current = index;

            while (layers[current].ParentIndex >= 0 && layers[current].ParentIndex < layers.Count)
                current = layers[current].ParentIndex;

            return current;
        }

        private static string GetFullPath(List<TemplateData.LayerSpec> layers, int index)
        {
            List<string> names = new List<string>();
            int current = index;

            while (current >= 0 && current < layers.Count)
            {
                names.Add(layers[current].Name);
                current = layers[current].ParentIndex;
            }

            names.Reverse();
            return string.Join("::", names);
        }

        private static Layer FindLayerByFullPath(RhinoDoc doc, string fullPath)
        {
            foreach (Layer layer in doc.Layers)
            {
                if (layer != null && !layer.IsDeleted && string.Equals(layer.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
                    return layer;
            }

            return null;
        }

        private static int FindLayerIndexByFullPath(RhinoDoc doc, string fullPath)
        {
            Layer layer = FindLayerByFullPath(doc, fullPath);
            return layer != null ? layer.Index : -1;
        }
    }
}
