using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatPV
{
    internal static class PVBake
    {
        public static void AddGeometryToDocument(RhinoDoc doc, PVGeometry geometry, PVParameters parameters)
        {
            if (geometry.PanelTransforms.Count == 0)
                return;

            var pvLayer = EnsureLayer(doc, "Numbat::PV::PV Surface", Color.FromArgb(166, 202, 168));
            var frameLayer = EnsureLayer(doc, "Numbat::PV::Frame", Color.FromArgb(155, 169, 181));
            var standLayer = EnsureLayer(doc, "Numbat::PV::Stand", Color.FromArgb(218, 196, 166));
            var parentLayer = EnsureLayer(doc, "Numbat::PV", Color.FromArgb(246, 217, 245));

            var blockGeometry = PVBuilder.CreatePanelBlockGeometry(parameters);
            var definitionGeometry = new List<GeometryBase>();
            var definitionAttributes = new List<ObjectAttributes>();

            foreach (var item in blockGeometry.PVSurface)
            {
                definitionGeometry.Add(item);
                definitionAttributes.Add(CreateLayerAttributes(pvLayer));
            }

            foreach (var item in blockGeometry.Frame)
            {
                definitionGeometry.Add(item);
                definitionAttributes.Add(CreateLayerAttributes(frameLayer));
            }

            foreach (var item in blockGeometry.Stand)
            {
                definitionGeometry.Add(item);
                definitionAttributes.Add(CreateLayerAttributes(standLayer));
            }

            var blockName = UniqueBlockName(doc, "Numbat_PV_Panel");
            var definitionIndex = doc.InstanceDefinitions.Add(blockName, "Numbat PV panel", Point3d.Origin, definitionGeometry, definitionAttributes);
            if (definitionIndex < 0)
                return;

            var instanceAttributes = CreateLayerAttributes(parentLayer);
            foreach (var transform in geometry.PanelTransforms)
                doc.Objects.AddInstanceObject(definitionIndex, transform, instanceAttributes);
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
