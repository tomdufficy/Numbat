using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Numbat.Commands.Analysis.NumbatProgramBlocks
{
    internal static class ProgramBlocksBuilder
    {
        private static readonly Random Random = new Random();

        private sealed class LayoutItem
        {
            public ProgramBlockRow Row { get; set; }
            public string CategoryName { get; set; }
            public Point3d LocalOrigin { get; set; }
        }

        private sealed class CreatedBlock
        {
            public Guid InstanceId { get; set; }
            public int DefinitionIndex { get; set; }
        }

        public static bool TryGetMetresPerDocumentUnit(RhinoDoc doc, out double metresPerUnit, out string error)
        {
            metresPerUnit = 0.0;
            error = null;

            if (doc.ModelUnitSystem == UnitSystem.None)
            {
                error = "Model units are not defined. Set the Rhino document units before generating program blocks.";
                return false;
            }

            if (doc.ModelUnitSystem == UnitSystem.CustomUnits)
            {
                try
                {
                    var method = typeof(RhinoDoc).GetMethod("GetCustomUnitSystem", new[]
                    {
                        typeof(bool),
                        typeof(string).MakeByRefType(),
                        typeof(double).MakeByRefType()
                    });

                    if (method == null)
                    {
                        error = "Could not access the document's custom unit scale.";
                        return false;
                    }

                    object[] arguments = { true, null, 0.0 };
                    object returnValue = method.Invoke(doc, arguments);
                    if (returnValue is bool success && !success)
                    {
                        error = "Could not read the document's custom unit scale.";
                        return false;
                    }

                    metresPerUnit = (double)arguments[2];
                }
                catch
                {
                    error = "Could not read the document's custom unit scale.";
                    return false;
                }
            }
            else
            {
                metresPerUnit = RhinoMath.MetersPerUnit(doc.ModelUnitSystem);
            }

            if (double.IsNaN(metresPerUnit) || double.IsInfinity(metresPerUnit) || metresPerUnit <= 0.0)
            {
                error = "The Rhino document has an invalid model-unit scale.";
                return false;
            }

            return true;
        }

        public static Result Create(
            RhinoDoc doc,
            IReadOnlyList<ProgramBlockRow> rows,
            ProgramBlocksOptions options,
            Point3d basePoint,
            double metresPerUnit)
        {
            var createdIds = new List<Guid>();
            var createdDefinitionIndices = new List<int>();
            var createdLayerIndices = new List<int>();
            uint undoSerial = 0;
            bool redrawWasEnabled = doc.Views.RedrawEnabled;

            try
            {
                if (doc.CurrentUndoRecordSerialNumber == 0)
                    undoSerial = doc.BeginUndoRecord("nbProgramBlocks");

                doc.Views.RedrawEnabled = false;

                int masterLayerIndex = EnsureMasterLayer(doc, createdLayerIndices);
                BuildLayout(rows, metresPerUnit, out var layout, out var categoryOrder);

                var categoryLayers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var assignedColours = new List<Color>();

                for (int i = 0; i < categoryOrder.Count; i++)
                {
                    string category = categoryOrder[i];
                    Color colour = GetCategoryColour(i, assignedColours);
                    int layerIndex = EnsureCategoryLayer(doc, category, colour, masterLayerIndex, createdLayerIndices);
                    if (layerIndex < 0)
                        throw new InvalidOperationException($"Invalid layer index returned for category '{category}'.");

                    categoryLayers[category] = layerIndex;
                }

                foreach (LayoutItem item in layout)
                {
                    Point3d origin = new Point3d(
                        basePoint.X + item.LocalOrigin.X,
                        basePoint.Y + item.LocalOrigin.Y,
                        basePoint.Z);

                    int layerIndex = categoryLayers[item.CategoryName];
                    CreatedBlock created = CreateProgramBlock(
                        doc,
                        item.Row,
                        layerIndex,
                        origin,
                        options,
                        metresPerUnit);

                    createdIds.Add(created.InstanceId);
                    createdDefinitionIndices.Add(created.DefinitionIndex);
                }

                doc.Objects.UnselectAll();
                foreach (Guid id in createdIds)
                    doc.Objects.Select(id);

                if (undoSerial != 0)
                {
                    if (!doc.EndUndoRecord(undoSerial))
                        throw new InvalidOperationException("Rhino could not complete the undo record.");
                    undoSerial = 0;
                }

                RhinoApp.WriteLine(
                    "nbProgramBlocks created {0} program block(s) across {1} category layer(s).",
                    createdIds.Count,
                    categoryOrder.Count);

                return Result.Success;
            }
            catch (Exception ex)
            {
                List<string> cleanupErrors = Rollback(doc, createdIds, createdDefinitionIndices, createdLayerIndices);

                if (undoSerial != 0)
                {
                    try { doc.EndUndoRecord(undoSerial); }
                    catch { }
                }

                string message = "Generation failed. Changes from this run were rolled back.\n\n" + ex.Message;
                if (cleanupErrors.Count > 0)
                {
                    message += "\n\nRhino reported cleanup issues:\n- " +
                               string.Join("\n- ", cleanupErrors.GetRange(0, Math.Min(5, cleanupErrors.Count)));
                }

                RhinoApp.WriteLine("nbProgramBlocks error: {0}", ex.Message);
                Rhino.UI.Dialogs.ShowMessage(message, "nbProgramBlocks");
                return Result.Failure;
            }
            finally
            {
                doc.Views.RedrawEnabled = redrawWasEnabled;
                doc.Views.Redraw();
            }
        }

        private static CreatedBlock CreateProgramBlock(
            RhinoDoc doc,
            ProgramBlockRow row,
            int categoryLayerIndex,
            Point3d origin,
            ProgramBlocksOptions options,
            double metresPerUnit)
        {
            ProgramBlocksData.GetRoomDimensions(row.AreaSquareMetres, out double widthMetres, out double heightMetres);

            double width = ToDocumentUnits(widthMetres, metresPerUnit);
            double height = ToDocumentUnits(heightMetres, metresPerUnit);
            double nameTextHeight = ToDocumentUnits(ProgramBlocksData.NameTextHeightMetres, metresPerUnit);
            double areaTextHeight = ToDocumentUnits(ProgramBlocksData.AreaTextHeightMetres, metresPerUnit);
            double lineOffset = ToDocumentUnits(0.18, metresPerUnit);

            var rectangle = new Rectangle3d(Plane.WorldXY, width, height).ToNurbsCurve();
            double centreX = width * 0.5;
            double centreY = height * 0.5;
            string areaValue = FormatArea(row.AreaSquareMetres);

            var geometries = new List<GeometryBase> { rectangle };
            var attributes = new List<ObjectAttributes> { CreateLayerAttributes(categoryLayerIndex) };

            if (options.IncludeVolume)
            {
                double volumeHeight = ToDocumentUnits(ProgramBlocksData.VolumeHeightMetres, metresPerUnit);
                Extrusion volume = Extrusion.Create(rectangle, volumeHeight, true);
                if (volume == null)
                    throw new InvalidOperationException($"Could not create the 3.5 m volume for '{row.Name}'.");

                geometries.Add(volume);
                attributes.Add(CreateLayerAttributes(categoryLayerIndex));
            }

            if (options.TextMode == ProgramTextMode.TextDot)
            {
                var dot = new TextDot(
                    row.Name + System.Environment.NewLine + areaValue + " m2",
                    new Point3d(centreX, centreY, 0.0));

                geometries.Add(dot);
                attributes.Add(CreateLayerAttributes(categoryLayerIndex));
            }
            else
            {
                var nameText = new TextEntity
                {
                    Plane = new Plane(
                        new Point3d(centreX, centreY + lineOffset, 0.0),
                        Vector3d.XAxis,
                        Vector3d.YAxis),
                    PlainText = row.Name,
                    TextHeight = nameTextHeight,
                    Justification = TextJustification.MiddleCenter
                };

                Plane areaPlane = new Plane(
                    new Point3d(centreX, centreY - lineOffset, 0.0),
                    Vector3d.XAxis,
                    Vector3d.YAxis);

                string richText = @"{\rtf1\ansi " + areaValue + @" m{\super 2}}";
                TextEntity areaText = TextEntity.CreateWithRichText(
                    richText,
                    areaPlane,
                    doc.DimStyles.Current,
                    false,
                    0.0,
                    0.0);

                if (areaText == null)
                {
                    areaText = new TextEntity
                    {
                        Plane = areaPlane,
                        PlainText = areaValue + " m2"
                    };
                }

                areaText.TextHeight = areaTextHeight;
                areaText.Justification = TextJustification.MiddleCenter;

                geometries.Add(nameText);
                geometries.Add(areaText);
                attributes.Add(CreateLayerAttributes(categoryLayerIndex));
                attributes.Add(CreateLayerAttributes(categoryLayerIndex));
            }

            string blockName = GetUniqueBlockName(doc, row.Name);
            string description = $"Program block: {row.Name} | {areaValue} m2 | {row.Category}";

            int definitionIndex = doc.InstanceDefinitions.Add(
                blockName,
                description,
                Point3d.Origin,
                geometries,
                attributes);

            if (definitionIndex < 0)
                throw new InvalidOperationException($"Rhino could not create the block definition for '{row.Name}'.");

            Transform transform = Transform.Translation(origin.X, origin.Y, origin.Z);
            Guid instanceId = doc.Objects.AddInstanceObject(
                definitionIndex,
                transform,
                CreateLayerAttributes(categoryLayerIndex));

            if (instanceId == Guid.Empty)
            {
                doc.InstanceDefinitions.Delete(definitionIndex, true, true);
                throw new InvalidOperationException($"Rhino could not insert the block instance for '{row.Name}'.");
            }

            return new CreatedBlock
            {
                InstanceId = instanceId,
                DefinitionIndex = definitionIndex
            };
        }

        private static void BuildLayout(
            IReadOnlyList<ProgramBlockRow> rows,
            double metresPerUnit,
            out List<LayoutItem> layout,
            out List<string> categoryOrder)
        {
            var byCategory = new Dictionary<string, List<ProgramBlockRow>>(StringComparer.OrdinalIgnoreCase);
            categoryOrder = new List<string>();

            foreach (ProgramBlockRow row in rows)
            {
                string category = row.CategoryName;
                if (!byCategory.TryGetValue(category, out List<ProgramBlockRow> categoryRows))
                {
                    categoryRows = new List<ProgramBlockRow>();
                    byCategory.Add(category, categoryRows);
                    categoryOrder.Add(category);
                }

                categoryRows.Add(row);
            }

            double roomGap = ToDocumentUnits(ProgramBlocksData.RoomGapMetres, metresPerUnit);
            double categoryGap = ToDocumentUnits(ProgramBlocksData.CategoryGapMetres, metresPerUnit);
            double rowTopY = 0.0;
            layout = new List<LayoutItem>();

            foreach (string category in categoryOrder)
            {
                var dimensions = new List<Tuple<ProgramBlockRow, double, double>>();
                double rowHeight = 0.0;

                foreach (ProgramBlockRow row in byCategory[category])
                {
                    ProgramBlocksData.GetRoomDimensions(row.AreaSquareMetres, out double widthMetres, out double heightMetres);
                    double width = ToDocumentUnits(widthMetres, metresPerUnit);
                    double height = ToDocumentUnits(heightMetres, metresPerUnit);
                    dimensions.Add(Tuple.Create(row, width, height));
                    rowHeight = Math.Max(rowHeight, height);
                }

                double x = 0.0;
                foreach (var item in dimensions)
                {
                    double y = rowTopY - item.Item3;
                    layout.Add(new LayoutItem
                    {
                        Row = item.Item1,
                        CategoryName = category,
                        LocalOrigin = new Point3d(x, y, 0.0)
                    });
                    x += item.Item2 + roomGap;
                }

                rowTopY -= rowHeight + categoryGap;
            }
        }

        private static int EnsureMasterLayer(RhinoDoc doc, List<int> createdLayerIndices)
        {
            int existing = FindLayer(doc, ProgramBlocksData.MasterLayerName, Guid.Empty);
            if (existing >= 0)
                return existing;

            var layer = new Layer { Name = ProgramBlocksData.MasterLayerName };
            int index = doc.Layers.Add(layer);
            if (index < 0)
                throw new InvalidOperationException($"Rhino could not create the '{ProgramBlocksData.MasterLayerName}' master layer.");

            createdLayerIndices.Add(index);
            return index;
        }

        private static int EnsureCategoryLayer(
            RhinoDoc doc,
            string category,
            Color colour,
            int masterLayerIndex,
            List<int> createdLayerIndices)
        {
            Layer master = doc.Layers[masterLayerIndex];
            if (master == null || master.IsDeleted)
                throw new InvalidOperationException($"The '{ProgramBlocksData.MasterLayerName}' master layer is unavailable.");

            string name = ProgramBlocksData.SafeName(category);
            int existing = FindLayer(doc, name, master.Id);
            if (existing >= 0)
                return existing;

            var layer = new Layer
            {
                Name = name,
                ParentLayerId = master.Id,
                Color = colour
            };

            int index = doc.Layers.Add(layer);
            if (index < 0)
                throw new InvalidOperationException(
                    $"Rhino could not create category layer '{ProgramBlocksData.MasterLayerName}::{name}'.");

            createdLayerIndices.Add(index);
            return index;
        }

        private static int FindLayer(RhinoDoc doc, string name, Guid parentId)
        {
            string target = ProgramBlocksData.SafeName(name);
            foreach (Layer layer in doc.Layers)
            {
                if (layer == null || layer.IsDeleted || layer.ParentLayerId != parentId)
                    continue;

                if (string.Equals(layer.Name, target, StringComparison.OrdinalIgnoreCase))
                    return layer.Index;
            }

            return -1;
        }

        private static ObjectAttributes CreateLayerAttributes(int layerIndex)
        {
            return new ObjectAttributes
            {
                LayerIndex = layerIndex,
                ColorSource = ObjectColorSource.ColorFromLayer
            };
        }

        private static string GetUniqueBlockName(RhinoDoc doc, string programName)
        {
            string baseName = "Program_" + ProgramBlocksData.SafeName(programName);
            string name = baseName;
            int suffix = 2;

            while (doc.InstanceDefinitions.Find(name) != null)
            {
                name = baseName + "_" + suffix;
                suffix++;
            }

            return name;
        }

        private static string FormatArea(double area)
        {
            return area.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static double ToDocumentUnits(double metres, double metresPerUnit)
        {
            return metres / metresPerUnit;
        }

        private static Color GetCategoryColour(int index, List<Color> assigned)
        {
            Color colour;
            if (index < ProgramBlocksData.CategoryColours.Length)
            {
                colour = ColorTranslator.FromHtml(ProgramBlocksData.CategoryColours[index]);
            }
            else
            {
                colour = GeneratePastel(assigned);
            }

            assigned.Add(colour);
            return colour;
        }

        private static Color GeneratePastel(List<Color> existing)
        {
            for (int attempt = 0; attempt < 250; attempt++)
            {
                double blend = 0.55 + Random.NextDouble() * 0.17;
                int r = (int)(Random.Next(256) * (1.0 - blend) + 255.0 * blend);
                int g = (int)(Random.Next(256) * (1.0 - blend) + 255.0 * blend);
                int b = (int)(Random.Next(256) * (1.0 - blend) + 255.0 * blend);
                Color candidate = Color.FromArgb(r, g, b);

                bool distinct = true;
                foreach (Color other in existing)
                {
                    double dr = candidate.R - other.R;
                    double dg = candidate.G - other.G;
                    double db = candidate.B - other.B;
                    if (Math.Sqrt(dr * dr + dg * dg + db * db) < 45.0)
                    {
                        distinct = false;
                        break;
                    }
                }

                if (distinct)
                    return candidate;
            }

            return Color.FromArgb(
                Random.Next(175, 246),
                Random.Next(175, 246),
                Random.Next(175, 246));
        }

        private static List<string> Rollback(
            RhinoDoc doc,
            List<Guid> createdIds,
            List<int> createdDefinitionIndices,
            List<int> createdLayerIndices)
        {
            var errors = new List<string>();

            for (int i = createdIds.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (!doc.Objects.Delete(createdIds[i], true))
                        errors.Add($"object cleanup failed for {createdIds[i]}");
                }
                catch (Exception ex)
                {
                    errors.Add("object cleanup: " + ex.Message);
                }
            }

            for (int i = createdDefinitionIndices.Count - 1; i >= 0; i--)
            {
                int index = createdDefinitionIndices[i];
                try
                {
                    InstanceDefinition definition = doc.InstanceDefinitions[index];
                    if (definition != null && !definition.IsDeleted &&
                        !doc.InstanceDefinitions.Delete(index, true, true))
                    {
                        errors.Add($"block definition cleanup failed at index {index}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add("block definition cleanup: " + ex.Message);
                }
            }

            for (int i = createdLayerIndices.Count - 1; i >= 0; i--)
            {
                int index = createdLayerIndices[i];
                try
                {
                    Layer layer = doc.Layers[index];
                    if (layer != null && !layer.IsDeleted && !doc.Layers.Delete(index, true))
                        errors.Add($"layer cleanup failed for '{layer.FullPath}'");
                }
                catch (Exception ex)
                {
                    errors.Add("layer cleanup: " + ex.Message);
                }
            }

            return errors;
        }
    }
}
