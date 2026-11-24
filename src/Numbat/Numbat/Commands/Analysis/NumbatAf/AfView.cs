using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.UI;
using Numbat.UI;
using SaveFileDialog = Eto.Forms.SaveFileDialog;
using SdColor = System.Drawing.Color;

namespace Numbat.Commands.Analysis.NumbatAf
{
    /// <summary>
    /// The content of the nbAF window, drawn with the Numbat UI kit, in the same layout as nbGFA: header with title
    /// and icon buttons, summary tiles, status callouts, then the buildings, the rule values and the colours, the
    /// Export and Bake buttons, and a footer with the status and copyright. Right-click (or Shift+F10) on a building
    /// opens its options. Everything is keyboard reachable.
    /// </summary>
    internal sealed class AfView : KitCanvas
    {
        private const float RowH = 58;
        private const float RowGap = 6;
        private const float ValueH = 34;
        private const float ChipH = 24;
        private const float ChipGap = 6;
        private const float ClashH = 28;

        private readonly AfModel _model;

        /// <summary>Raised when interface size or outlines change, so the window can resize.</summary>
        public event EventHandler DisplayChanged;

        public AfView(AfModel model)
        {
            _model = model;
            HelpRequested += (s, e) => AfHelp.Show();
        }

        /// <summary>Visible height of the scroller in control pixels, so the footer can sit at the bottom.</summary>
        public Func<float> ViewportHeight { get; set; } = () => 0;

        protected override bool DrawnTips => true;

        private static KitPalette C => Kit.Colours;

        private static RhinoDoc Doc => RhinoDoc.ActiveDoc;

        // ---- layout --------------------------------------------------------------------------

        private readonly KitSections _sections = new KitSections(AfInfo.SettingsPrefix + "closed");

        private float W => Width > 0 ? LogicalWidth : 330;

        private static float FooterH => Kit.Pad + 2 * KitPaint.LineHeight(KitFonts.Caption) + 4 + Kit.Pad;

        private static float TilesH => Math.Max(KitPaint.TileHeight(true, true), KitPaint.TileHeight(false, true));

        private const string EmptyText =
            "No buildings yet. Select storey volumes and click Add. Volumes that touch, or are in one Rhino group, form one building.";

        private const string NoRoadsText =
            "No road centre lines. Abstandsflächen may reach a public road only up to its middle (§ 6 Abs. 2): select the centre " +
            "lines of the roads around the site and click Add, and nbAF flags any that go further.";

        /// <summary>Status lines: no plots, deleted plots, near-misses, open objects, skipped faces, changed values.</summary>
        private List<(CalloutKind kind, string text, string link, Action click, string tip)> Callouts()
        {
            var lines = new List<(CalloutKind, string, string, Action, string)>();
            if (_model.Buildings.Count > 0 && _model.Buildings.All(b => b.Plot == Guid.Empty))
                lines.Add((CalloutKind.Info, "No plots drawn, so plots aren't checked: only overlapping Abstandsflächen and areas reaching into buildings are flagged.",
                    null, null, null));
            foreach (Building b in _model.Buildings.Where(b => b.PlotMissing))
            {
                Building building = b;
                lines.Add((CalloutKind.Warning, $"The plot curve of {b.Name} was deleted.", "Set plot",
                    () => SetPlot(Doc, building), $"Pick a new plot for {b.Name}"));
            }
            if (_model.NearMisses.Count > 0)
            {
                NearMiss first = _model.NearMisses[0];
                string text = _model.NearMisses.Count == 1
                    ? $"{first.A.Name} and {first.B.Name} are {first.GapText} apart. One building or two?"
                    : $"{_model.NearMisses.Count} pairs of buildings are almost touching (closest: {first.A.Name} and {first.B.Name}, {first.GapText}). One building or two?";
                lines.Add((CalloutKind.Warning, text, "Review", () => NearMissDialog.Show(ParentWindow, _model, Doc),
                    "Join them into one building, keep them apart, or select them to look"));
            }
            if (_model.OpenVolumes.Count > 0)
            {
                lines.Add((CalloutKind.Warning, $"{_model.OpenVolumes.Count} tracked object(s) aren't closed volumes and are left out.", "Select",
                    () => SelectIds(Doc, _model.OpenVolumes), "Select them to cap or join them"));
            }
            if ((_model.Current?.LeaningWalls ?? 0) > 0)
            {
                lines.Add((CalloutKind.Info, $"{_model.Current.LeaningWalls} wall face(s) lean 2–15° off vertical and get no Abstandsfläche.", null, null, null));
            }
            if (_model.SkippedFaces > 0)
                lines.Add((CalloutKind.Warning, $"{_model.SkippedFaces} curved wall face(s) skipped. Only flat walls get Abstandsflächen.", null, null, null));
            if (!_model.Rules.IsDefault)
                lines.Add((CalloutKind.Info, "Values differ from the HBauO defaults.", "Reset values", ResetRules,
                    "Bring back the HBauO § 6 values"));
            return lines;
        }

        private int ClashCount => _model.Current?.ClashCount ?? 0;

        /// <summary>Height the content needs at a width, in design px. The window scrolls when it is taller.</summary>
        public float ContentHeight(float w) => Measure(g => Body(g, w)) + Kit.Pad + FooterH;

        /// <summary>Makes the drawable tall enough for its content and repaints.</summary>
        public void UpdateHeight()
        {
            int needed = (int)Math.Ceiling(ContentHeight(W) * S);
            if (Height != needed) Height = needed;
            Invalidate();
        }

        // ---- painting ------------------------------------------------------------------------

        protected override void PaintContent(Graphics g)
        {
            float w = W;
            float bottom = Body(g, w);
            DrawFooter(g, w, bottom);
        }

        /// <summary>Lays out and paints everything above the footer. Returns where the content ends.</summary>
        private float Body(Graphics g, float w)
        {
            DrawHeader(g, w);
            float y = Kit.HeaderH + Kit.Pad;
            DrawTiles(g, w, y);
            y += TilesH + Kit.Pad;

            var callouts = Callouts();
            for (int i = 0; i < callouts.Count; i++)
            {
                var c = callouts[i];
                float h = KitPaint.CalloutHeight(w - 2 * Kit.Pad, c.text, c.link != null);
                string id = "callout" + i;
                KitPaint.Callout(g, new RectangleF(Kit.Pad, y, w - 2 * Kit.Pad, h), c.kind, c.text, c.link, StateOf(id), out RectangleF link);
                if (c.click != null) AddTarget(id, link, c.click, null, c.tip);
                y += h + Kit.Gap;
            }
            if (callouts.Count > 0) y += 4;

            // The two view switches sit on top, where they are used most.
            Switch(g, Kit.Pad, y, "buildings", "Colour by building", AfColours.ColourByBuilding, v => AfColours.ColourByBuilding = v,
                "Tint each building in its own colour and label it, to check what nbAF counts as one building");
            Switch(g, w / 2 + 4, y, "labels", "Depth labels", AfColours.ShowLabels, v => AfColours.ShowLabels = v,
                AfColours.ShowLabels ? "Hide the depth dimensions in the viewports" : "Show a depth dimension for each wall in the viewports (and bake them)");
            y += Kit.ControlH + Kit.Gap;

            y = DrawBuildings(g, w, y);
            y = DrawRoads(g, w, y + Kit.Pad);
            if ((_model.Current?.Flags.Count ?? 0) > 0) y = DrawClashes(g, w, y + Kit.Pad);
            y = DrawModel(g, w, y + Kit.Pad);
            y = DrawValues(g, w, y + Kit.Pad);
            y = DrawColours(g, w, y + Kit.Pad);
            y += Kit.Pad + Kit.Gap;
            DrawActions(g, w, y);
            return y + Kit.ControlH;
        }

        /// <summary>A collapsible section header. Returns whether the section is open.</summary>
        private bool Section(Graphics g, float y, string key, string title, string count = null)
        {
            bool open = _sections.IsOpen(key);
            RectangleF hit = KitPaint.SectionToggle(g, Kit.Pad, y, title, open, StateOf("s:" + key), count);
            AddTarget("s:" + key, hit, () =>
            {
                _sections.Toggle(key);
                UpdateHeight();
            }, null, open ? $"Close {title.ToLowerInvariant()}" : $"Open {title.ToLowerInvariant()}");
            return open;
        }

        private void DrawHeader(Graphics g, float w)
        {
            KitPaint.Header(g, w, null, AfInfo.Name);

            RectangleF more = KitPaint.HeaderButton(w, 0);
            RectangleF help = KitPaint.HeaderButton(w, 1);

            KitPaint.Button(g, help, null, ButtonKind.Ghost, StateOf("help"), KitIcon.Help);
            AddTarget("help", help, AfHelp.Show, null, "How to use nbAF (F1)");

            KitPaint.Button(g, more, null, ButtonKind.Ghost, StateOf("more"), KitIcon.More);
            AddTarget("more", more, () => ShowMainMenu(this, ToControl(new PointF(more.X, more.Bottom + 2))), null, "More options");
        }

        private void DrawTiles(Graphics g, float w, float y)
        {
            float inner = w - 2 * Kit.Pad - Kit.Gap;
            float w1 = (float)Math.Floor(inner * 0.58f);
            int clashes = ClashCount;
            int checks = (_model.Current?.Flags.Count ?? 0) - clashes;

            var r1 = new RectangleF(Kit.Pad, y, w1, TilesH);
            if (_model.Buildings.Count == 0)
                KitPaint.Tile(g, r1, "Clashes", "–", "No buildings", big: true);
            else
                KitPaint.Tile(g, r1, "Clashes", clashes == 0 ? "None" : clashes.ToString(),
                    clashes == 0 ? (checks > 0 ? $"{checks} to check" : "All checks pass") : "Numbered in the model",
                    big: true, subColor: clashes == 0 ? (checks > 0 ? C.Warn : C.Ok) : C.Err);
            if (clashes > 0) KitIcons.Draw(g, KitIcon.Warning, new RectangleF(r1.Right - 8 - 14, r1.Y + 8, 14, 14), C.Err);
            AddTarget("clashes", r1, null, null,
                "Every building is checked against every other: overlapping Abstandsflächen, areas reaching into a building, areas reaching another building's plot, and areas past a road's centre line.");

            var r2 = new RectangleF(Kit.Pad + w1 + Kit.Gap, y, inner - w1, TilesH);
            string zone = _model.Rules.Zone == Zone.Commercial ? "GE/GI zone" : "General zone";
            KitPaint.Tile(g, r2, "Buildings", _model.Buildings.Count.ToString(), zone);
        }

        private float DrawBuildings(Graphics g, float w, float y)
        {
            bool open = Section(g, y, "buildings", "Buildings", _model.Buildings.Count > 0 ? _model.Buildings.Count.ToString() : null);
            float addW = KitPaint.ButtonWidth("Add", KitIcon.Plus);
            float removeW = KitPaint.ButtonWidth("Remove", KitIcon.Minus);
            var add = new RectangleF(w - Kit.Pad - addW, y, addW, Kit.ControlH);
            var remove = new RectangleF(add.X - Kit.Gap - removeW, y, removeW, Kit.ControlH);
            KitPaint.Button(g, remove, "Remove", ButtonKind.Ghost, StateOf("remove"), KitIcon.Minus);
            AddTarget("remove", remove, () => RemoveSelected(Doc), null, "Stop tracking the selected volumes (they are not deleted)");
            KitPaint.Button(g, add, "Add", ButtonKind.Secondary, StateOf("add"), KitIcon.Plus);
            AddTarget("add", add, () => AddSelected(Doc), null, "Track the selected volumes, or pick them if nothing is selected");
            y += Kit.ControlH + Kit.Gap;
            if (!open) return y - Kit.Gap;

            if (_model.Buildings.Count == 0) return WrappedText(g, w, y, EmptyText) + Kit.Gap;
            foreach (Building b in _model.Buildings)
            {
                DrawBuilding(g, new RectangleF(Kit.Pad, y, w - 2 * Kit.Pad, RowH), b);
                y += RowH + RowGap;
            }
            return y;
        }

        private float WrappedText(Graphics g, float w, float y, string text)
        {
            float lh = KitPaint.LineHeight(KitFonts.Body);
            foreach (string line in KitPaint.Wrap(KitFonts.Body, text, w - 2 * Kit.Pad))
            {
                g.DrawText(KitFonts.Body, C.Text3, Kit.Pad, y, line);
                y += lh;
            }
            return y;
        }

        private void DrawBuilding(Graphics g, RectangleF rect, Building b)
        {
            KitPalette c = C;
            string id = "b:" + b.Name;
            PartState st = StateOf(id);
            Building building = b;

            // The row itself first, so its buttons (added later) win the hit test.
            AddTarget(id, rect, () => SelectVolumes(Doc, building), p => ShowBuildingMenu(Doc, building, p),
                $"{b.Name}: click to select its volumes, right-click for options");

            KitPaint.Fill(g, rect, Kit.RadiusCard, st.Pressed ? c.Border : st.Hover ? c.Surface2 : c.Surface);
            KitPaint.Stroke(g, rect, Kit.RadiusCard, b.Flags > 0 ? c.Err : c.Border, b.Flags > 0 ? 1.5f : 1);
            if (st.Focused) KitPaint.FocusRing(g, rect, Kit.RadiusCard);

            float left = rect.X + 12, right = rect.Right - 10;
            // The building's own colour when colouring by building, so the row matches the viewport.
            Color swatch = Kit.ToEto(AfColours.ColourByBuilding ? AfColours.BuildingColour(b.ColourIndex) : AfColours.Get(Element.Areas));
            KitPaint.Fill(g, new RectangleF(left, rect.Y + 14, 10, 10), 2, swatch);

            string plotText = b.PlotMissing ? "Plot missing" : b.Plot != Guid.Empty ? "Plot" : "Set plot";
            KitIcon plotIcon = b.PlotMissing ? KitIcon.Warning : b.Plot != Guid.Empty ? KitIcon.Check : KitIcon.Pick;
            float plotW = KitPaint.ButtonWidth(plotText, plotIcon);
            var plotRect = new RectangleF(right - plotW, rect.Y + (RowH - Kit.ControlH) / 2, plotW, Kit.ControlH);
            KitPaint.Button(g, plotRect, plotText, ButtonKind.Secondary, StateOf(id + ":plot"), plotIcon);
            AddTarget(id + ":plot", plotRect, () => SetPlot(Doc, building), p => ShowBuildingMenu(Doc, building, p),
                b.PlotMissing ? $"The plot curve of {b.Name} was deleted: click to pick a new one"
                : b.Plot != Guid.Empty ? $"{b.Name} has a plot: click to pick another, right-click for options"
                : $"Use the selected closed curve as {b.Name}'s plot, or pick one");

            float textW = plotRect.X - 8 - (left + 18);
            KitPaint.TextMiddle(g, KitFonts.Label, c.Text, left + 18, rect.Y + 9, 20, KitPaint.Fit(KitFonts.Label, b.Name, textW));
            string detail = $"{b.Volumes.Count} vol.";
            float line2 = rect.Y + 31;
            g.DrawText(KitFonts.Caption, c.Text3, left, line2, detail);
            if (b.Flags > 0)
            {
                float dx = KitPaint.Measure(KitFonts.Caption, detail + " · ").Width;
                g.DrawText(KitFonts.Caption, c.Err, left + dx, line2,
                    KitPaint.Fit(KitFonts.Caption, $"{b.Flags} clash{(b.Flags == 1 ? "" : "es")}", plotRect.X - 8 - left - dx));
            }
        }

        private float DrawRoads(Graphics g, float w, float y)
        {
            KitPalette c = C;
            List<Guid> roads = _model.Roads;
            bool open = Section(g, y, "roads", "Road centre lines", roads.Count > 0 ? roads.Count.ToString() : null);
            float addW = KitPaint.ButtonWidth("Add", KitIcon.Plus);
            var add = new RectangleF(w - Kit.Pad - addW, y, addW, Kit.ControlH);
            KitPaint.Button(g, add, "Add", ButtonKind.Secondary, StateOf("road:add"), KitIcon.Plus);
            AddTarget("road:add", add, () => AddRoads(Doc), null, "Use the selected curves as road centre lines, or pick them");
            y += Kit.ControlH + Kit.Gap;
            if (!open) return y - Kit.Gap;

            if (roads.Count == 0) return WrappedText(g, w, y, NoRoadsText) + Kit.Gap;
            for (int i = 0; i < roads.Count; i++)
            {
                Guid road = roads[i];
                string id = "road:" + i;
                var rect = new RectangleF(Kit.Pad, y, w - 2 * Kit.Pad, ClashH);
                PartState st = StateOf(id);
                AddTarget(id, rect, () => SelectIds(Doc, new[] { road }), null, "Click to select this centre line");
                if (st.Hover || st.Pressed) KitPaint.Fill(g, rect, Kit.RadiusControl, st.Pressed ? c.Border : c.Surface2);
                if (st.Focused) KitPaint.FocusRing(g, rect, Kit.RadiusControl);

                var remove = new RectangleF(rect.Right - Kit.ControlH, rect.Y, Kit.ControlH, Kit.ControlH);
                KitPaint.Button(g, remove, null, ButtonKind.Ghost, StateOf(id + ":x"), KitIcon.Close);
                AddTarget(id + ":x", remove, () => _model.RemoveRoad(Doc, road), null, "Stop using this curve as a road centre line (it isn't deleted)");

                g.FillRectangle(Kit.ToEto(AfColours.Get(Element.Roads)), rect.X + 6, rect.Y + ClashH / 2 - 1, 14, 2);
                double length = (Doc?.Objects.FindId(road)?.Geometry as Curve)?.GetLength() ?? 0;
                double metre = Doc == null ? 1 : RhinoMath.UnitScale(UnitSystem.Meters, Doc.ModelUnitSystem);
                KitPaint.TextMiddle(g, KitFonts.Body, c.Text, rect.X + 28, rect.Y, ClashH, $"Road {i + 1} · {length / metre:0.0} m");
                y += ClashH;
            }
            return y;
        }

        /// <summary>Model settings: the join gap (not an HBauO value, so not under RULE VALUES).</summary>
        private float DrawModel(Graphics g, float w, float y)
        {
            AfRules rules = _model.Rules;
            RuleField f = AfRules.JoinGapField;
            bool open = Section(g, y, "model", "Model");
            y += Kit.ControlH;
            if (!open) return y;
            bool changed = !f.IsDefault(rules);
            ValueRow(g, w, y, f.Key, f.Label, f.Paragraph, f.Format(f.Get(rules)), changed,
                changed ? $"changed · default {f.Format(f.Default)}" : null, false, () =>
                {
                    double? edited = RuleDialog.Ask(ParentWindow, f, f.Get(rules));
                    if (!edited.HasValue) return;
                    f.Set(rules, edited.Value);
                    _model.SaveRules(Doc, rules);
                }, "Volumes closer than this count as touching, so a tiny modelling gap doesn't split a building in two");
            return y + ValueH;
        }

        private float DrawValues(Graphics g, float w, float y)
        {
            AfRules rules = _model.Rules;
            bool open = Section(g, y, "values", "Rule values");
            if (!rules.IsDefault)
            {
                float resetW = KitPaint.ButtonWidth("Reset", KitIcon.Reset);
                var reset = new RectangleF(w - Kit.Pad - resetW, y, resetW, Kit.ControlH);
                KitPaint.Button(g, reset, "Reset", ButtonKind.Ghost, StateOf("reset"), KitIcon.Reset);
                AddTarget("reset", reset, ResetRules, null, "Bring back the HBauO § 6 values");
            }
            y += Kit.ControlH;
            if (!open) return y;

            // Zone first: it decides which factor is used.
            bool commercial = rules.Zone == Zone.Commercial;
            ValueRow(g, w, y, "zone", "Zone", "§ 6 Abs. 5", commercial ? "GE/GI" : "General", commercial,
                commercial ? "changed · default General" : null, false, () =>
                {
                    rules.Zone = commercial ? Zone.General : Zone.Commercial;
                    _model.SaveRules(Doc, rules);
                }, "Switch between the general rule and Gewerbe- and Industriegebiete (GE/GI)");
            y += ValueH;

            foreach (RuleField f in AfRules.Fields)
            {
                RuleField field = f;
                bool changed = !f.IsDefault(rules);
                bool unused = rules.Unused(f);
                ValueRow(g, w, y, f.Key, f.Label, f.Paragraph, f.Format(f.Get(rules)), changed,
                    changed ? $"changed · default {f.Format(f.Default)}" : unused ? "not used in this zone" : null, unused, () =>
                    {
                        double? edited = RuleDialog.Ask(ParentWindow, field, field.Get(rules));
                        if (!edited.HasValue) return;
                        field.Set(rules, edited.Value);
                        _model.SaveRules(Doc, rules);
                    }, $"Change {f.Label.ToLowerInvariant()} (saved in this file)");
                y += ValueH;
            }
            return y;
        }

        /// <summary>One value: label and paragraph (or a note) on the left, the value as a button on the right.</summary>
        private void ValueRow(Graphics g, float w, float y, string key, string label, string paragraph, string value,
            bool changed, string note, bool unused, Action click, string tip)
        {
            KitPalette c = C;
            string id = "v:" + key;
            float valueW = Math.Max(64, KitPaint.ButtonWidth(value));
            var box = new RectangleF(w - Kit.Pad - valueW, y + 1, valueW, Kit.ControlH);
            PartState st = StateOf(id);
            st.On = changed;
            KitPaint.Button(g, box, value, ButtonKind.Secondary, st);
            AddTarget(id, new RectangleF(Kit.Pad, y, w - 2 * Kit.Pad, ValueH), click, null, tip);

            float room = box.X - 8 - Kit.Pad;
            float lh = KitPaint.LineHeight(KitFonts.Body);
            float top = y + (ValueH - lh - KitPaint.LineHeight(KitFonts.Caption)) / 2;
            g.DrawText(KitFonts.Body, unused ? c.Text3 : c.Text, Kit.Pad, top, KitPaint.Fit(KitFonts.Body, label, room));
            g.DrawText(KitFonts.Caption, changed ? c.Warn : c.Text3, Kit.Pad, top + lh,
                KitPaint.Fit(KitFonts.Caption, note ?? paragraph, room));
        }

        /// <summary>Colour chips flowed into rows from <paramref name="top"/>.</summary>
        private static List<(Element element, RectangleF rect)> LayoutChips(float w, float top)
        {
            var chips = new List<(Element, RectangleF)>();
            float x = Kit.Pad, y = top;
            foreach (var e in AfColours.Elements)
            {
                float width = Math.Min(KitPaint.ChipWidth(e.label), w - 2 * Kit.Pad);
                if (x + width > w - Kit.Pad && x > Kit.Pad)
                {
                    x = Kit.Pad;
                    y += ChipH + ChipGap;
                }
                chips.Add((e.element, new RectangleF(x, y, width, ChipH)));
                x += width + ChipGap;
            }
            return chips;
        }

        private float DrawColours(Graphics g, float w, float y)
        {
            bool open = Section(g, y, "display", "Colours");
            y += Kit.ControlH;
            if (!open) return y;
            float bottom = y;
            foreach (var chip in LayoutChips(w, y))
            {
                Element element = chip.element;
                string id = "c:" + element;
                KitPaint.Chip(g, chip.rect, Kit.ToEto(AfColours.Get(element)), AfColours.Label(element), StateOf(id));
                AddTarget(id, chip.rect, () => EditColour(element), null,
                    $"Change the colour of {AfColours.Label(element).ToLowerInvariant()} in the preview, new baked layers and exports");
                bottom = Math.Max(bottom, chip.rect.Bottom);
            }
            return bottom;
        }

        private void Switch(Graphics g, float x, float y, string id, string label, bool on, Action<bool> set, string tip)
        {
            float rowW = 28 + 8 + KitPaint.Measure(KitFonts.Body, label).Width;
            KitPaint.Switch(g, new PointF(x, y + (Kit.ControlH - 16) / 2), on, StateOf(id));
            KitPaint.TextMiddle(g, KitFonts.Body, C.Text, x + 28 + 8, y, Kit.ControlH, label);
            AddTarget(id, new RectangleF(x, y, rowW, Kit.ControlH), () =>
            {
                set(!on);
                Doc?.Views.Redraw();
                Invalidate();
            }, null, tip);
        }

        private float DrawClashes(Graphics g, float w, float y)
        {
            KitPalette c = C;
            var flags = _model.Current.Flags;
            int clashes = ClashCount, checks = flags.Count - clashes;
            string count = clashes > 0 && checks > 0 ? $"{clashes} + {checks} to check" : clashes > 0 ? clashes.ToString() : $"{checks} to check";
            bool open = Section(g, y, "clashes", "Clashes", count);
            y += Kit.ControlH;
            if (!open) return y;
            foreach (Flag f in flags)
            {
                Flag flag = f;
                string id = "f:" + f.Number;
                var rect = new RectangleF(Kit.Pad, y, w - 2 * Kit.Pad, ClashH);
                PartState st = StateOf(id);
                if (st.Hover || st.Pressed) KitPaint.Fill(g, rect, Kit.RadiusControl, st.Pressed ? c.Border : c.Surface2);

                // Numbered marker, as in the viewports: red for a clash, amber for something to check.
                string number = f.Number.ToString();
                float nw = Math.Max(20, KitPaint.Measure(KitFonts.Caption, number).Width + 10);
                var marker = new RectangleF(rect.X + 4, rect.Y + (ClashH - 18) / 2, nw, 18);
                KitPaint.Fill(g, marker, 9, f.IsCheck ? Kit.ToEto(AfColours.CheckColour) : Kit.ToEto(AfColours.Get(Element.Clashes)));
                SizeF ns = KitPaint.Measure(KitFonts.Caption, number);
                g.DrawText(KitFonts.Caption, Colors.White, marker.X + (nw - ns.Width) / 2, marker.Y + (18 - ns.Height) / 2, number);

                string area = f.IsCheck ? "check" : $"{f.Area:0.0} m²";
                float areaW = KitPaint.Measure(KitFonts.Caption, area).Width;
                KitPaint.TextMiddle(g, KitFonts.Caption, c.Text3, rect.Right - 6 - areaW, rect.Y, ClashH, area);
                float x = marker.Right + 8;
                KitPaint.TextMiddle(g, KitFonts.Body, c.Text, x, rect.Y, ClashH, KitPaint.Fit(KitFonts.Body, f.Text, rect.Right - 6 - areaW - 8 - x));
                if (st.Focused) KitPaint.FocusRing(g, rect, Kit.RadiusControl);
                // The hover tip explains which rule it breaks, with the numbers.
                AddTarget(id, rect, () => ZoomTo(flag), null, $"{f.Number}. {f.Text}: {f.Why}{(f.Note == null ? "" : $" ({f.Note})")} Click to zoom to it.");
                y += ClashH;
            }
            return y;
        }

        private static void ZoomTo(Flag f)
        {
            RhinoDoc doc = Doc;
            Rhino.Display.RhinoView view = doc?.Views.ActiveView;
            if (view == null) return;
            BoundingBox box = f.Region.GetBoundingBox(true);
            box.Inflate(Math.Max(box.Diagonal.X, box.Diagonal.Y) * 1.5);
            view.ActiveViewport.ZoomBoundingBox(box);
            view.Redraw();
        }

        private void DrawActions(Graphics g, float w, float y)
        {
            bool any = _model.Current != null && _model.Buildings.Count > 0;
            float bakeW = KitPaint.ButtonWidth("Bake", KitIcon.Layer);
            float exportW = KitPaint.ButtonWidth("Export", KitIcon.ChevronDown);
            var bake = new RectangleF(w - Kit.Pad - bakeW, y, bakeW, Kit.ControlH);
            var export = new RectangleF(bake.X - Kit.Gap - exportW, y, exportW, Kit.ControlH);

            KitPaint.Button(g, export, "Export", ButtonKind.Secondary, StateOf("export", any), KitIcon.ChevronDown);
            AddTarget("export", export, () => ShowExportMenu(Doc, new PointF(export.X, export.Bottom + 2)), null,
                "Save a 2D top view as DXF, SVG or JPG", any);
            KitPaint.Button(g, bake, "Bake", ButtonKind.Primary, StateOf("bake", any), KitIcon.Layer);
            AddTarget("bake", bake, () => RhinoApp.RunScript("_nbAFBake", false), null,
                "Add the Abstandsflächen to the nbAF layers, replacing what was baked before", any);
        }

        private void DrawFooter(Graphics g, float w, float bodyBottom)
        {
            float viewport = ViewportHeight() / S;
            float top = Math.Max(bodyBottom + Kit.Pad, viewport - FooterH);
            KitPaint.HLine(g, 0, w, top, C.Border);

            float y = top + Kit.Pad;
            float lh = KitPaint.LineHeight(KitFonts.Caption);
            float statusW = w - 2 * Kit.Pad;
            int clashes = ClashCount;
            int checks = (_model.Current?.Flags.Count ?? 0) - clashes;
            string values = _model.Rules.IsDefault ? "" : " · values changed";
            string toCheck = checks > 0 ? $" · {checks} to check" : "";
            if (_model.Buildings.Count == 0)
                KitPaint.StatusLine(g, Kit.Pad, y, C.Outline, "No buildings tracked", statusW);
            else if (clashes > 0)
                KitPaint.StatusLine(g, Kit.Pad, y, C.Err, $"{clashes} clash{(clashes == 1 ? "" : "es")}{toCheck}{values}", statusW);
            else
                KitPaint.StatusLine(g, Kit.Pad, y, checks > 0 ? C.Warn : C.Ok, $"No clashes{toCheck}{values}", statusW);
            y += lh + 4;

            g.DrawText(KitFonts.Caption, C.Text3, Kit.Pad, y, AfInfo.Copyright);
        }

        // ---- interaction ---------------------------------------------------------------------

        private static List<RhinoObject> SelectedVolumes(RhinoDoc doc) =>
            doc == null ? new List<RhinoObject>()
                        : doc.Objects.GetSelectedObjects(false, false)
                             .Where(o => (o.Geometry is Brep b && b.IsSolid) || (o.Geometry is Extrusion x && x.IsSolid)).ToList();

        private void AddSelected(RhinoDoc doc)
        {
            if (doc == null) return;
            List<RhinoObject> volumes = SelectedVolumes(doc);
            if (volumes.Count == 0)
            {
                RhinoApp.RunScript("_nbAFAdd", false); // nothing selected: pick on the command line
                return;
            }
            int n = _model.Add(doc, volumes);
            RhinoApp.WriteLine($"{AfInfo.Name}: added {n} volume(s).");
        }

        /// <summary>Uses the selected curves as road centre lines, or asks for them on the command line.</summary>
        private void AddRoads(RhinoDoc doc)
        {
            if (doc == null) return;
            var curves = doc.Objects.GetSelectedObjects(false, false).Where(o => o.Geometry is Curve).ToList();
            if (curves.Count == 0)
            {
                RhinoApp.RunScript("_nbAFAddRoad", false);
                return;
            }
            int n = _model.AddRoads(doc, curves);
            RhinoApp.WriteLine($"{AfInfo.Name}: {n} road centre line(s) added.");
        }

        private void RemoveSelected(RhinoDoc doc)
        {
            if (doc == null) return;
            List<RhinoObject> volumes = SelectedVolumes(doc).Where(o => AfModel.IsMember(o.Attributes)).ToList();
            if (volumes.Count == 0)
            {
                RhinoApp.WriteLine($"{AfInfo.Name}: select tracked volumes first, then click Remove.");
                return;
            }
            int n = _model.Remove(doc, volumes);
            RhinoApp.WriteLine($"{AfInfo.Name}: stopped tracking {n} volume(s). The objects themselves are not deleted.");
        }

        /// <summary>Uses the one selected closed curve as the plot, or else asks for one on the command line.</summary>
        private void SetPlot(RhinoDoc doc, Building b)
        {
            if (doc == null) return;
            var curves = doc.Objects.GetSelectedObjects(false, false)
                            .Where(o => o.Geometry is Curve c && c.IsClosed && c.IsPlanar()).ToList();
            if (curves.Count == 1)
            {
                _model.SetPlot(doc, b, curves[0]);
                RhinoApp.WriteLine($"{AfInfo.Name}: plot of {b.Name} set.");
            }
            else
            {
                RhinoApp.RunScript($"_nbAFSetPlot {b.Name}", false);
            }
        }

        private static void SelectIds(RhinoDoc doc, IEnumerable<Guid> ids)
        {
            if (doc == null) return;
            doc.Objects.UnselectAll();
            foreach (Guid id in ids) doc.Objects.Select(id);
            doc.Views.Redraw();
        }

        private void SelectVolumes(RhinoDoc doc, Building b)
        {
            if (doc == null) return;
            doc.Objects.UnselectAll();
            foreach (RhinoObject o in _model.VolumesOf(doc, b)) o.Select(true);
            doc.Views.Redraw();
        }

        private void ResetRules()
        {
            AfRules rules = _model.Rules;
            rules.ResetToDefaults();
            _model.SaveRules(Doc, rules);
        }

        private void EditColour(Element element)
        {
            SdColor c = AfColours.Get(element);
            if (!Dialogs.ShowColorDialog(ref c)) return;
            AfColours.Set(element, c);
            Doc?.Views.Redraw();
            Invalidate();
        }

        private void ShowBuildingMenu(RhinoDoc doc, Building b, PointF location)
        {
            if (doc == null) return;
            var menu = new ContextMenu();
            menu.Items.Add(Item($"Select {b.Name}'s volumes", true, () => SelectVolumes(doc, b)));
            menu.Items.Add(new SeparatorMenuItem());
            menu.Items.Add(Item("Set plot…", true, () => SetPlot(doc, b)));
            menu.Items.Add(Item("Select plot", b.Plot != Guid.Empty && !b.PlotMissing, () =>
            {
                doc.Objects.UnselectAll();
                doc.Objects.Select(b.Plot);
                doc.Views.Redraw();
            }));
            menu.Items.Add(Item("Clear plot", b.Plot != Guid.Empty, () => _model.SetPlot(doc, b, null)));
            menu.Items.Add(new SeparatorMenuItem());
            menu.Items.Add(Item($"Stop tracking {b.Name}", true, () => _model.Remove(doc, _model.VolumesOf(doc, b))));
            menu.Show(this, ToControl(location));
        }

        /// <summary>The window's ⋯ menu, opened from the title bar at a point in <paramref name="owner"/>'s pixels.</summary>
        public void ShowMainMenu(Control owner, PointF location)
        {
            RhinoDoc doc = Doc;
            if (doc == null) return;
            var menu = new ContextMenu();
            menu.Items.Add(Item("How to use nbAF", true, AfHelp.Show));

            var sources = new SubMenuItem { Text = "Sources" };
            foreach (AfSource s in AfSources.All)
            {
                AfSource source = s;
                var item = new SubMenuItem { Text = s.Title };
                if (s.File != null)
                    item.Items.Add(Item($"Open saved copy ({s.Accessed})", s.HasLocalCopy, () => AfSources.Open(source.LocalPath)));
                item.Items.Add(Item("Open online", true, () => AfSources.Open(source.Url)));
                sources.Items.Add(item);
            }
            menu.Items.Add(sources);

            menu.Items.Add(new SeparatorMenuItem());
            menu.Items.Add(Item("Reset values to HBauO defaults", !_model.Rules.IsDefault, ResetRules));
            menu.Items.Add(Item("Reset colours", true, () =>
            {
                AfColours.Reset();
                doc.Views.Redraw();
                Invalidate();
            }));
            menu.Items.Add(Item("Update now", true, () => _model.Refresh(doc)));
            menu.Items.Add(Item("Delete baked output…", true, () => RhinoApp.RunScript("_nbAFClear", false)));

            menu.Items.Add(new SeparatorMenuItem());
            Display.AddMenuItems(menu, () => DisplayChanged?.Invoke(this, EventArgs.Empty));
            menu.Show(owner, location);
        }

        private void ShowExportMenu(RhinoDoc doc, PointF location)
        {
            if (doc == null) return;
            ExportOptions o = ExportOptions.Load();
            var menu = new ContextMenu();
            CheckMenuItem Option(string text, bool value, Action<bool> set, bool enabled = true)
            {
                var item = new CheckMenuItem { Text = text, Checked = value, Enabled = enabled };
                item.Click += (s, e) =>
                {
                    set(item.Checked);
                    o.Save();
                };
                return item;
            }
            menu.Items.Add(Option("Clash zones", o.Clashes, v => o.Clashes = v));
            menu.Items.Add(Option("Clash numbers", o.Numbers, v => o.Numbers = v, o.Clashes));
            menu.Items.Add(Option("Clash notes", o.Notes, v => o.Notes = v, o.Clashes));
            menu.Items.Add(Option("Depth dimensions", o.Dimensions, v => o.Dimensions = v));
            menu.Items.Add(new SeparatorMenuItem());
            menu.Items.Add(Item("Export DXF…", true, () => Export(doc, "dxf")));
            menu.Items.Add(Item("Export SVG…", true, () => Export(doc, "svg")));
            menu.Items.Add(Item("Export JPG…", true, () => Export(doc, "jpg")));
            menu.Show(this, ToControl(location));
        }

        private void Export(RhinoDoc doc, string format)
        {
            if (_model.Current == null) return;
            string name = string.IsNullOrEmpty(doc.Name) ? "Abstandsflaechen" : Path.GetFileNameWithoutExtension(doc.Name) + " Abstandsflaechen";
            var dialog = new SaveFileDialog { Title = $"{AfInfo.Name} – Export {format.ToUpperInvariant()}", FileName = $"{name}.{format}" };
            dialog.Filters.Add(new FileFilter(format.ToUpperInvariant(), "." + format));
            if (dialog.ShowDialog(ParentWindow) != DialogResult.Ok) return;
            string path = dialog.FileName;
            if (!path.EndsWith("." + format, StringComparison.OrdinalIgnoreCase)) path += "." + format;

            try
            {
                ExportData data = AfExporter.Collect(_model.Current, _model.Rules, ExportOptions.Load(), doc);
                if (format == "dxf") AfExporter.WriteDxf(path, data);
                else if (format == "svg") AfExporter.WriteSvg(path, data);
                else AfExporter.WriteJpg(path, data);
                RhinoApp.WriteLine($"{AfInfo.Name}: exported {path}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ParentWindow, $"Export failed: {ex.Message}", AfInfo.Name, MessageBoxType.Error);
            }
        }

        private static ButtonMenuItem Item(string text, bool enabled, Action action)
        {
            var item = new ButtonMenuItem { Text = text, Enabled = enabled };
            item.Click += (s, e) => action();
            return item;
        }
    }
}
