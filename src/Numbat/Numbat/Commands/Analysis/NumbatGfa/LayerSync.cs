using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rhino;
using Rhino.DocObjects;

namespace Numbat.Commands.Analysis.NumbatGfa
{
    /// <summary>
    /// The layer tree used in By layer mode:
    ///   GFA
    ///     Residential, Office, ...   (one sublayer per use; nested layers below them count too)
    ///     Unassigned
    /// </summary>
    internal static class LayerSync
    {
        public const string UnassignedName = "Unassigned";
        private static readonly Color RootColor = Color.FromArgb(120, 120, 130);
        private static readonly Color UnassignedColor = Color.FromArgb(190, 190, 195);

        public static Layer FindRoot(RhinoDoc doc)
        {
            int index = doc.Layers.FindByFullPath(GfaInfo.RootLayer, -1);
            if (index < 0) return null;
            Layer layer = doc.Layers[index];
            return layer.IsDeleted ? null : layer;
        }

        public static Layer EnsureRoot(RhinoDoc doc)
        {
            Layer root = FindRoot(doc);
            if (root != null) return root;
            int index = Checked(doc.Layers.Add(new Layer { Name = GfaInfo.RootLayer, Color = RootColor }), GfaInfo.RootLayer);
            return doc.Layers[index];
        }

        public static int EnsureUnassigned(RhinoDoc doc)
        {
            Layer root = EnsureRoot(doc);
            int index = doc.Layers.FindByFullPath($"{GfaInfo.RootLayer}::{UnassignedName}", -1);
            if (index >= 0 && !doc.Layers[index].IsDeleted) return index;
            return Checked(doc.Layers.Add(new Layer { Name = UnassignedName, Color = UnassignedColor, ParentLayerId = root.Id }), UnassignedName);
        }

        public static int FindUnassigned(RhinoDoc doc)
        {
            int index = doc.Layers.FindByFullPath($"{GfaInfo.RootLayer}::{UnassignedName}", -1);
            return index >= 0 && !doc.Layers[index].IsDeleted ? index : -1;
        }

        /// <summary>The use's layer, created if needed. Updates <see cref="UseCategory.LayerId"/>.</summary>
        public static int EnsureUseLayer(RhinoDoc doc, UseCategory use)
        {
            Layer root = EnsureRoot(doc);

            Layer byId = use.LayerId == Guid.Empty ? null : doc.Layers.FindId(use.LayerId);
            if (byId != null && !byId.IsDeleted && byId.ParentLayerId == root.Id) return byId.Index;

            int index = doc.Layers.FindByFullPath($"{GfaInfo.RootLayer}::{use.Name}", -1);
            if (index < 0 || doc.Layers[index].IsDeleted)
            {
                index = Checked(doc.Layers.Add(new Layer { Name = use.Name, Color = use.Color, ParentLayerId = root.Id }), use.Name);
            }
            use.LayerId = doc.Layers[index].Id;
            return index;
        }

        /// <summary>Layers.Add returns -1 on failure; fail loudly instead of indexing with it.</summary>
        private static int Checked(int index, string name)
        {
            if (index < 0) throw new InvalidOperationException($"{GfaInfo.Name}: could not create the layer \"{name}\".");
            return index;
        }

        public static Layer FindUseLayer(RhinoDoc doc, UseCategory use)
        {
            if (use.LayerId == Guid.Empty) return null;
            Layer layer = doc.Layers.FindId(use.LayerId);
            return layer == null || layer.IsDeleted ? null : layer;
        }

        /// <summary>Direct sublayers of the GFA layer, except Unassigned.</summary>
        public static List<Layer> UseLayers(RhinoDoc doc, Layer root) =>
            doc.Layers
                .Where(l => !l.IsDeleted && l.ParentLayerId == root.Id &&
                            !string.Equals(l.Name, UnassignedName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(l => l.SortIndex)
                .ToList();

        /// <summary>
        /// Maps every layer index that counts as a use (a use layer or any layer nested below it) to its use.
        /// </summary>
        public static Dictionary<int, UseCategory> BuildMap(RhinoDoc doc, Layer root, IEnumerable<UseCategory> uses)
        {
            var map = new Dictionary<int, UseCategory>();
            if (root == null) return map;

            var byLayerId = uses.Where(u => u.LayerId != Guid.Empty)
                .GroupBy(u => u.LayerId).ToDictionary(g => g.Key, g => g.First());
            var layers = doc.Layers.Where(l => !l.IsDeleted).ToDictionary(l => l.Id);

            foreach (Layer layer in layers.Values)
            {
                // Walk up until we reach a direct child of the GFA layer.
                Layer current = layer;
                int guard = 0;
                while (current != null && current.ParentLayerId != root.Id && guard++ < 64)
                {
                    layers.TryGetValue(current.ParentLayerId, out current);
                }
                if (current != null && byLayerId.TryGetValue(current.Id, out UseCategory use))
                {
                    map[layer.Index] = use;
                }
            }
            return map;
        }

        public static void SetLayerName(RhinoDoc doc, Layer layer, string name)
        {
            layer.Name = name;
            doc.Layers.Modify(layer, layer.Index, true);
        }

        public static void SetLayerColor(RhinoDoc doc, Layer layer, Color color)
        {
            layer.Color = color;
            doc.Layers.Modify(layer, layer.Index, true);
        }
    }
}
