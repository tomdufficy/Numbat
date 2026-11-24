using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Render;

namespace Numbat.Commands.Utilities.NumbatAnon
{
    /// <summary>Where in a Rhino file nbAnon looks. Each is one switch in the window.</summary>
    internal enum AnonArea { Layers, Blocks, Objects, Text, Groups, Document, Views, Styles, Materials, Files }

    /// <summary>Turns found text into its anonymised version. <c>isName</c>: the result must not be empty.</summary>
    internal delegate string Rewrite(string text, bool isName);

    /// <summary>One place where the phrase was found.</summary>
    internal sealed class AnonHit
    {
        public AnonArea Area;
        /// <summary>What and where, for the report: "Layer · Acme::Walls · name".</summary>
        public string Where;
        public string Text;
        /// <summary>False for places nbAnon can only report (file paths, snapshots).</summary>
        public bool Editable = true;
        public bool Include = true;
        public bool IsName;
        /// <summary>Text after anonymising, filled in for the preview and the report.</summary>
        public string After;
        /// <summary>What each phrase becomes in this place (its own set when every place gets its own).</summary>
        public IReadOnlyList<string> With;
        /// <summary>The random code a name gets if replacing would leave it empty.</summary>
        public string Fallback;
        internal Action<RhinoDoc, Rewrite> Apply;
    }

    /// <summary>
    /// Finds a phrase everywhere a Rhino file keeps text: layer names and layer user text; block names, descriptions,
    /// linked file paths and every object inside every block definition (nested blocks are definitions too, so all
    /// levels are covered); object names, hyperlinks, attribute and geometry user text; text, text dots, leaders and
    /// dimension text; group names; document user text and notes; named views, construction planes, positions,
    /// layer states, snapshots and layout names; linetypes, hatch patterns and dimension styles; materials,
    /// environments and textures; the file's own path and embedded bitmap names.
    /// Each hit knows how to change itself (<see cref="AnonHit.Apply"/>).
    /// </summary>
    internal static class AnonScanner
    {
        public static readonly IReadOnlyList<(AnonArea area, string label, string tip)> Areas = new[]
        {
            (AnonArea.Layers, "Layers", "Layer names, at every level, and layer user text"),
            (AnonArea.Blocks, "Blocks", "Block names and descriptions, and everything inside every block definition: nested blocks, object names, user text and text"),
            (AnonArea.Objects, "Objects", "Object names, hyperlinks, attribute user text and geometry user text, also on hidden and locked objects"),
            (AnonArea.Text, "Text", "Text, text dots, leaders and dimension text overrides"),
            (AnonArea.Groups, "Groups", "Group names"),
            (AnonArea.Document, "Document", "Document user text (Document Properties > User Text) and notes"),
            (AnonArea.Views, "Views", "Named views, named CPlanes, named positions, layer states, snapshots and layout names"),
            (AnonArea.Styles, "Styles", "Linetype, hatch pattern and dimension style names"),
            (AnonArea.Materials, "Materials", "Material, environment and texture names"),
            (AnonArea.Files, "File paths", "The file's own path, linked block files and embedded bitmaps. Reported only: change these by renaming or re-linking the files."),
        };

        public static List<AnonHit> Scan(RhinoDoc doc, AnonMatcher m, ICollection<AnonArea> areas)
        {
            var hits = new List<AnonHit>();
            void Hit(AnonArea area, string where, string text, bool isName, Action<RhinoDoc, Rewrite> apply)
            {
                if (!areas.Contains(area) || !m.IsMatch(text)) return;
                hits.Add(new AnonHit { Area = area, Where = where, Text = text, IsName = isName, Apply = apply, Editable = apply != null });
            }

            ScanLayers(doc, Hit);
            ScanBlocks(doc, m, areas, hits, Hit);
            ScanObjects(doc, Hit);
            ScanGroups(doc, Hit);
            ScanDocument(doc, Hit);
            ScanViews(doc, Hit);
            ScanStyles(doc, Hit);
            ScanMaterials(doc, Hit);
            ScanFiles(doc, Hit);
            return hits;
        }

        private delegate void HitSink(AnonArea area, string where, string text, bool isName, Action<RhinoDoc, Rewrite> apply);

        // ---- layers -----------------------------------------------------------------------

        private static void ScanLayers(RhinoDoc doc, HitSink hit)
        {
            foreach (Layer layer in doc.Layers.Where(l => !l.IsDeleted && !l.IsReference))
            {
                int index = layer.Index;
                hit(AnonArea.Layers, $"Layer · {layer.FullPath} · name", layer.Name, true, (d, rw) =>
                {
                    Layer l = d.Layers[index];
                    string name = Unique(Valid(rw(l.Name, true), ModelComponent.IsValidComponentName), n => SiblingExists(d, l, n));
                    if (name == l.Name) return;
                    l.Name = name;
                    d.Layers.Modify(l, index, true);
                });
                ScanUserStrings(layer.GetUserStrings(), $"Layer · {layer.FullPath} · user text", AnonArea.Layers, hit,
                    (d, rw, oldKey, isKey) =>
                    {
                        Layer l = d.Layers[index];
                        string value = l.GetUserString(oldKey);
                        string key = isKey ? rw(oldKey, true) : oldKey;
                        if (!isKey) value = rw(value, false);
                        if (key != oldKey) l.DeleteUserString(oldKey);
                        l.SetUserString(key, value);
                        d.Layers.Modify(l, index, true);
                    });
            }
        }

        private static bool SiblingExists(RhinoDoc doc, Layer layer, string name) =>
            doc.Layers.Any(o => !o.IsDeleted && o.Index != layer.Index && o.ParentLayerId == layer.ParentLayerId &&
                                string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));

        // ---- blocks ------------------------------------------------------------------------

        private static void ScanBlocks(RhinoDoc doc, AnonMatcher m, ICollection<AnonArea> areas, List<AnonHit> hits, HitSink hit)
        {
            foreach (InstanceDefinition idef in doc.InstanceDefinitions.Where(i => i != null && !i.IsDeleted && !i.IsReference))
            {
                int index = idef.Index;
                string label = $"Block · {idef.Name}";
                hit(AnonArea.Blocks, $"{label} · name", idef.Name, true, (d, rw) =>
                {
                    InstanceDefinition def = d.InstanceDefinitions[index];
                    string name = Unique(Valid(rw(def.Name, true), n => n.Length > 0), n => d.InstanceDefinitions.Find(n) != null);
                    if (name != def.Name) d.InstanceDefinitions.Modify(index, name, def.Description, true);
                });
                hit(AnonArea.Blocks, $"{label} · description", idef.Description, false, (d, rw) =>
                {
                    InstanceDefinition def = d.InstanceDefinitions[index];
                    d.InstanceDefinitions.Modify(index, def.Name, rw(def.Description, false), true);
                });
                hit(AnonArea.Files, $"{label} · linked file", idef.SourceArchive, false, null);

                // Everything inside the definition. Each change rebuilds the definition's geometry, so it re-reads
                // the current objects first: earlier changes to the same block are kept.
                RhinoObject[] inside = idef.GetObjects() ?? Array.Empty<RhinoObject>();
                for (int k = 0; k < inside.Length; k++)
                {
                    int slot = k;
                    string where = $"{label} › object {k + 1} ({inside[k].ObjectType})";
                    void Edit(RhinoDoc d, Action<GeometryBase[], ObjectAttributes[]> change)
                    {
                        RhinoObject[] objs = d.InstanceDefinitions[index].GetObjects();
                        if (objs == null || slot >= objs.Length) return;
                        GeometryBase[] geo = objs.Select(o => o.Geometry.Duplicate()).ToArray();
                        ObjectAttributes[] attr = objs.Select(o => o.Attributes.Duplicate()).ToArray();
                        change(geo, attr);
                        d.InstanceDefinitions.ModifyGeometry(index, geo, attr);
                    }
                    ScanObject(inside[k], where, AnonArea.Blocks, AnonArea.Blocks, hit, Edit, slot);
                }
            }
        }

        // ---- objects ------------------------------------------------------------------------

        private static void ScanObjects(RhinoDoc doc, HitSink hit)
        {
            var settings = new ObjectEnumeratorSettings
            {
                NormalObjects = true, LockedObjects = true, HiddenObjects = true, IncludeLights = true,
                IncludeGrips = false, ReferenceObjects = false,
            };
            foreach (RhinoObject obj in doc.Objects.GetObjectList(settings))
            {
                Guid id = obj.Id;
                string where = $"{obj.ObjectType}{(string.IsNullOrEmpty(obj.Attributes.Name) ? "" : $" '{obj.Attributes.Name}'")} on {doc.Layers[obj.Attributes.LayerIndex].FullPath}";
                void Edit(RhinoDoc d, Action<GeometryBase[], ObjectAttributes[]> change)
                {
                    RhinoObject o = d.Objects.FindId(id);
                    if (o == null) return;
                    var geo = new[] { o.Geometry.Duplicate() };
                    var attr = new[] { o.Attributes.Duplicate() };
                    string before = Snapshot(geo[0]);
                    change(geo, attr);
                    d.Objects.ModifyAttributes(o, attr[0], true);
                    if (Snapshot(geo[0]) != before)
                    {
                        ReplaceGeometry(d, id, geo[0]);
                    }
                }
                ScanObject(obj, where, AnonArea.Objects, AnonArea.Text, hit, Edit, 0);
            }
        }

        private static void ReplaceGeometry(RhinoDoc d, Guid id, GeometryBase g)
        {
            switch (g)
            {
                case TextEntity t: d.Objects.Replace(id, t); break;
                case TextDot t: d.Objects.Replace(id, t); break;
                case Leader l: d.Objects.Replace(id, l); break;
                default: d.Objects.Replace(id, g, true); break;
            }
        }

        /// <summary>
        /// Names, hyperlinks, user text and text of one object (in the model, or slot <paramref name="slot"/> of a
        /// block definition). <paramref name="edit"/> runs a change on fresh copies of its geometry and attributes.
        /// </summary>
        private static void ScanObject(RhinoObject obj, string where, AnonArea attributesArea, AnonArea textArea, HitSink hit,
            Action<RhinoDoc, Action<GeometryBase[], ObjectAttributes[]>> edit, int slot)
        {
            ObjectAttributes a = obj.Attributes;
            hit(attributesArea, $"{where} · name", a.Name, false, (d, rw) => edit(d, (g, at) => at[slot].Name = rw(at[slot].Name, false)));
            hit(attributesArea, $"{where} · hyperlink", a.Url, false, (d, rw) => edit(d, (g, at) => at[slot].Url = rw(at[slot].Url, false)));
            ScanUserStrings(a.GetUserStrings(), $"{where} · user text", attributesArea, hit,
                (d, rw, oldKey, isKey) => edit(d, (g, at) =>
                {
                    string value = at[slot].GetUserString(oldKey);
                    string key = isKey ? rw(oldKey, true) : oldKey;
                    if (!isKey) value = rw(value, false);
                    if (key != oldKey) at[slot].DeleteUserString(oldKey);
                    at[slot].SetUserString(key, value);
                }));
            ScanUserStrings(obj.Geometry.GetUserStrings(), $"{where} · geometry user text", attributesArea, hit,
                (d, rw, oldKey, isKey) => edit(d, (g, at) =>
                {
                    string value = g[slot].GetUserString(oldKey);
                    string key = isKey ? rw(oldKey, true) : oldKey;
                    if (!isKey) value = rw(value, false);
                    if (key != oldKey) g[slot].SetUserString(oldKey, null);
                    g[slot].SetUserString(key, value);
                }));

            switch (obj.Geometry)
            {
                case AnnotationBase ann:
                    hit(textArea, $"{where} · text", ann.PlainText, false, (d, rw) => edit(d, (g, at) => RewriteAnnotation((AnnotationBase)g[slot], rw)));
                    break;
                case TextDot dot:
                    hit(textArea, $"{where} · dot text", dot.Text, false, (d, rw) => edit(d, (g, at) => ((TextDot)g[slot]).Text = rw(((TextDot)g[slot]).Text, true)));
                    hit(textArea, $"{where} · dot secondary text", dot.SecondaryText, false, (d, rw) => edit(d, (g, at) => ((TextDot)g[slot]).SecondaryText = rw(((TextDot)g[slot]).SecondaryText, false)));
                    break;
            }
        }

        /// <summary>Changes annotation text, keeping its formatting where the phrase isn't split by it.</summary>
        private static void RewriteAnnotation(AnnotationBase ann, Rewrite rw)
        {
            string plain = ann.PlainText;
            string wanted = rw(plain, false);
            if (wanted == plain) return;
            if (ann.TextHasRtfFormatting)
            {
                string rich = rw(ann.RichText, false);
                ann.RichText = rich;
                if (ann.PlainText == wanted) return;
            }
            ann.PlainText = wanted;
        }

        /// <summary>Geometry user text and text content, to tell whether a change touched the geometry.</summary>
        private static string Snapshot(GeometryBase g)
        {
            NameValueCollection strings = g.GetUserStrings();
            string user = strings == null ? "" : string.Join("\n", strings.AllKeys.Select(k => k + "=" + strings[k]));
            return user + "\n" + Text(g);
        }

        private static string Text(GeometryBase g) => g is AnnotationBase a ? a.RichText : g is TextDot t ? t.Text + "\n" + t.SecondaryText : "";

        // ---- user text (shared) --------------------------------------------------------------

        private delegate void UserStringEdit(RhinoDoc d, Rewrite rw, string oldKey, bool isKey);

        /// <summary>One hit for each key or value that contains the phrase.</summary>
        private static void ScanUserStrings(NameValueCollection strings, string where, AnonArea area, HitSink hit, UserStringEdit edit)
        {
            if (strings == null) return;
            foreach (string key in strings.AllKeys)
            {
                if (key == null) continue;
                string k = key;
                hit(area, $"{where} · key", k, true, (d, rw) => edit(d, rw, k, true));
                hit(area, $"{where} · {k}", strings[key], false, (d, rw) => edit(d, rw, k, false));
            }
        }

        // ---- groups, document, views -------------------------------------------------------------

        private static void ScanGroups(RhinoDoc doc, HitSink hit)
        {
            for (int i = 0; i < doc.Groups.Count; i++)
            {
                string name = doc.Groups.GroupName(i);
                if (name == null) continue;
                int index = i;
                hit(AnonArea.Groups, $"Group · {name}", name, true, (d, rw) =>
                    d.Groups.ChangeGroupName(index, Unique(rw(d.Groups.GroupName(index), true), n => d.Groups.Find(n) >= 0)));
            }
        }

        private static void ScanDocument(RhinoDoc doc, HitSink hit)
        {
            for (int i = 0; i < doc.Strings.Count; i++)
            {
                string key = doc.Strings.GetKey(i), value = doc.Strings.GetValue(i);
                if (key == null) continue;
                hit(AnonArea.Document, "Document user text · key", key, true, (d, rw) =>
                {
                    string v = d.Strings.GetValue(key);
                    d.Strings.Delete(key);
                    d.Strings.SetString(rw(key, true), v);
                });
                hit(AnonArea.Document, $"Document user text · {key}", value, false, (d, rw) =>
                    d.Strings.SetString(key, rw(d.Strings.GetValue(key), false)));
            }
            hit(AnonArea.Document, "Document notes", doc.Notes, false, (d, rw) => d.Notes = rw(d.Notes, false));
        }

        private static void ScanViews(RhinoDoc doc, HitSink hit)
        {
            for (int i = 0; i < doc.NamedViews.Count; i++)
            {
                string name = doc.NamedViews[i].Name;
                hit(AnonArea.Views, $"Named view · {name}", name, true, (d, rw) =>
                {
                    int index = d.NamedViews.FindByName(name);
                    if (index >= 0) d.NamedViews.Rename(index, Unique(rw(name, true), n => d.NamedViews.FindByName(n) >= 0));
                });
            }
            for (int i = 0; i < doc.NamedConstructionPlanes.Count; i++)
            {
                string name = doc.NamedConstructionPlanes[i].Name;
                hit(AnonArea.Views, $"Named CPlane · {name}", name, true, (d, rw) =>
                {
                    // No rename in RhinoCommon: replace it with a copy under the new name.
                    int index = d.NamedConstructionPlanes.Find(name);
                    if (index < 0) return;
                    ConstructionPlane cp = d.NamedConstructionPlanes[index];
                    string newName = Unique(rw(name, true), n => d.NamedConstructionPlanes.Find(n) >= 0);
                    d.NamedConstructionPlanes.Delete(index);
                    d.NamedConstructionPlanes.Add(newName, cp.Plane);
                });
            }
            foreach (string name in doc.NamedPositions.Names ?? Array.Empty<string>())
            {
                hit(AnonArea.Views, $"Named position · {name}", name, true, (d, rw) => d.NamedPositions.Rename(name, Unique(rw(name, true), n => (d.NamedPositions.Names ?? Array.Empty<string>()).Contains(n))));
            }
            foreach (string name in doc.NamedLayerStates.Names ?? Array.Empty<string>())
            {
                hit(AnonArea.Views, $"Layer state · {name}", name, true, (d, rw) => d.NamedLayerStates.Rename(name, Unique(rw(name, true), n => (d.NamedLayerStates.Names ?? Array.Empty<string>()).Contains(n))));
            }
            foreach (string name in doc.Snapshots.Names ?? Array.Empty<string>())
            {
                hit(AnonArea.Views, $"Snapshot · {name}", name, true, null); // no rename in RhinoCommon: rename in the Snapshots panel
            }
            foreach (Rhino.Display.RhinoPageView page in doc.Views.GetPageViews() ?? Array.Empty<Rhino.Display.RhinoPageView>())
            {
                Guid id = page.MainViewport.Id;
                hit(AnonArea.Views, $"Layout · {page.PageName}", page.PageName, true, (d, rw) =>
                {
                    Rhino.Display.RhinoPageView p = d.Views.GetPageViews().FirstOrDefault(v => v.MainViewport.Id == id);
                    if (p != null) p.PageName = rw(p.PageName, true);
                });
            }
        }

        // ---- styles, materials, files ----------------------------------------------------------------

        private static void ScanStyles(RhinoDoc doc, HitSink hit)
        {
            foreach (Linetype lt in doc.Linetypes.Where(l => !l.IsDeleted && !l.IsReference))
            {
                int index = lt.Index;
                hit(AnonArea.Styles, $"Linetype · {lt.Name}", lt.Name, true, (d, rw) =>
                {
                    Linetype l = d.Linetypes[index];
                    l.Name = Unique(rw(l.Name, true), n => d.Linetypes.Find(n) >= 0);
                    d.Linetypes.Modify(l, index, true);
                });
            }
            foreach (HatchPattern hp in doc.HatchPatterns.Where(h => !h.IsDeleted && !h.IsReference))
            {
                int index = hp.Index;
                hit(AnonArea.Styles, $"Hatch pattern · {hp.Name}", hp.Name, true, (d, rw) =>
                    d.HatchPatterns.Rename(index, Unique(rw(d.HatchPatterns[index].Name, true), n => d.HatchPatterns.FindName(n) != null)));
            }
            foreach (DimensionStyle ds in doc.DimStyles.Where(s => !s.IsDeleted && !s.IsReference))
            {
                Guid id = ds.Id;
                hit(AnonArea.Styles, $"Dimension style · {ds.Name}", ds.Name, true, (d, rw) =>
                {
                    DimensionStyle s = d.DimStyles.FindId(id);
                    if (s == null) return;
                    DimensionStyle copy = s.Duplicate();
                    copy.Name = Unique(rw(s.Name, true), n => d.DimStyles.FindName(n) != null);
                    d.DimStyles.Modify(copy, id, true);
                });
            }
        }

        private static void ScanMaterials(RhinoDoc doc, HitSink hit)
        {
            for (int i = 0; i < doc.Materials.Count; i++)
            {
                Material mat = doc.Materials[i];
                if (mat == null || mat.IsDeleted) continue;
                int index = i;
                hit(AnonArea.Materials, $"Material · {mat.Name}", mat.Name, true, (d, rw) =>
                {
                    Material m = d.Materials[index];
                    m.Name = rw(m.Name, true);
                    d.Materials.Modify(m, index, true);
                });
            }
            void Contents(IEnumerable<RenderContent> list, string kind)
            {
                foreach (RenderContent c in list ?? Enumerable.Empty<RenderContent>())
                {
                    Guid id = c.Id;
                    hit(AnonArea.Materials, $"{kind} · {c.Name}", c.Name, true, (d, rw) =>
                    {
                        RenderContent rc = RenderContent.FromId(d, id);
                        if (rc == null) return;
                        rc.BeginChange(RenderContent.ChangeContexts.Program);
                        rc.Name = rw(rc.Name, true);
                        rc.EndChange();
                    });
                }
            }
            Contents(doc.RenderMaterials, "Render material");
            Contents(doc.RenderEnvironments, "Environment");
            Contents(doc.RenderTextures, "Texture");
        }

        private static void ScanFiles(RhinoDoc doc, HitSink hit)
        {
            hit(AnonArea.Files, "This file · path", doc.Path, false, null);
            foreach (BitmapEntry b in doc.Bitmaps)
            {
                if (b != null) hit(AnonArea.Files, "Embedded bitmap · file name", b.FileName, false, null);
            }
        }

        // ---- helpers -----------------------------------------------------------------------------

        /// <summary>The name, or a random code when it isn't valid (e.g. it became empty).</summary>
        private static string Valid(string name, Func<string, bool> isValid) =>
            !string.IsNullOrWhiteSpace(name) && isValid(name) ? name : AnonWords.Letters();

        /// <summary>The name, with " 2", " 3", ... added while it is taken.</summary>
        private static string Unique(string name, Func<string, bool> taken)
        {
            if (string.IsNullOrWhiteSpace(name)) name = AnonWords.Letters();
            if (!taken(name)) return name;
            for (int i = 2; ; i++)
            {
                string candidate = $"{name} {i}";
                if (!taken(candidate)) return candidate;
            }
        }
    }
}
