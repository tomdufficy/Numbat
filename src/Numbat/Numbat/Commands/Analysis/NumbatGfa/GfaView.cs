using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Font = Eto.Drawing.Font;
using Numbat.UI;
using SdColor = System.Drawing.Color;

namespace Numbat.Commands.Analysis.NumbatGfa
{
    /// <summary>
    /// The content of the nbGFA window, drawn with the Numbat UI kit: header with title and icon buttons, summary
    /// tiles, status callouts, then the uses as bars or a pie chart, and a footer. Clicking a use assigns the
    /// selected volumes; right-click (or Shift+F10) opens its options. Everything is keyboard reachable.
    /// </summary>
    internal sealed class GfaView : KitCanvas
    {
        private const float RowH = 58;
        private const float RowGap = 6;

        private const float PieR = 46;
        private const float LabelH = 32;
        private const float LabelGap = 4;
        private const float LeaderGap = 14;
        private const float ChipH = 24;

        private readonly GfaModel _model;
        private PieLayout _pieLayout;

        /// <summary>Raised when the bars / pie view changes, so the window can save it.</summary>
        public event EventHandler ViewChanged;

        public GfaView(GfaModel model, bool pie)
        {
            _model = model;
            Pie = pie;
            HelpRequested += (s, e) => HelpWindow.ShowWindow();
        }

        public bool Pie { get; private set; }

        /// <summary>Visible height of the scroller in control pixels, so the footer can sit at the bottom.</summary>
        public Func<float> ViewportHeight { get; set; } = () => 0;

        protected override bool DrawnTips => true;

        private static KitPalette C => Kit.Colours;

        // ---- layout --------------------------------------------------------------------------

        private float W => Width > 0 ? LogicalWidth : 330;

        private static float FooterH =>
            Kit.Pad + 2 * KitPaint.LineHeight(KitFonts.Caption) + 4 + Kit.Pad;

        /// <summary>Extra status lines: units warning, volume issues.</summary>
        private List<(CalloutKind kind, string text, string link, Action click)> Callouts(Tally t)
        {
            var lines = new List<(CalloutKind, string, string, Action)>();
            if (t.UnitWarning != null) lines.Add((CalloutKind.Warning, t.UnitWarning, null, null));
            if (t.Issues.Count > 0)
            {
                string notCounted = t.NotCountedCount > 0 ? $", {t.NotCountedCount} not counted" : string.Empty;
                lines.Add((CalloutKind.Warning, $"{t.Issues.Count} volume issue(s){notCounted}.", "Review",
                    () => IssuesDialog.Show(ParentWindow, _model, RhinoDoc.ActiveDoc)));
            }
            return lines;
        }

        private static float TilesH => Math.Max(KitPaint.TileHeight(true, false), KitPaint.TileHeight(false, true));

        /// <summary>Top of the uses (bars or pie) at a width.</summary>
        private float BodyTop(float w)
        {
            float y = Kit.HeaderH + Kit.Pad + TilesH + Kit.Pad;
            var callouts = Callouts(_model.Current);
            foreach (var c in callouts) y += KitPaint.CalloutHeight(w - 2 * Kit.Pad, c.text, c.link != null) + Kit.Gap;
            if (callouts.Count > 0) y += 4;
            return y + Kit.ControlH + Kit.Gap; // section header row
        }

        private float BodyHeight(float w)
        {
            if (_model.Categories.Count == 0) return KitPaint.LineHeight(KitFonts.Body) + Kit.Gap;
            return Pie ? LayoutPie(_model.Current, BodyTop(w), w).Height : _model.Categories.Count * (RowH + RowGap);
        }

        /// <summary>Height the content needs at a width, in design px. The window scrolls when it is taller.</summary>
        public float ContentHeight(float w) => BodyTop(w) + BodyHeight(w) + Kit.Pad + FooterH;

        /// <summary>Makes the drawable tall enough for its content and repaints.</summary>
        public void UpdateHeight()
        {
            int needed = (int)Math.Ceiling(ContentHeight(W) * S);
            if (Height != needed) Height = needed;
            Invalidate();
        }

        public void ToggleView()
        {
            Pie = !Pie;
            ViewChanged?.Invoke(this, EventArgs.Empty);
            UpdateHeight();
        }

        // ---- painting ------------------------------------------------------------------------

        protected override void PaintContent(Graphics g)
        {
            Tally t = _model.Current;
            float w = W;

            DrawHeader(g, w);
            float y = Kit.HeaderH + Kit.Pad;
            DrawTiles(g, t, w, y);
            y += TilesH + Kit.Pad;

            var callouts = Callouts(t);
            for (int i = 0; i < callouts.Count; i++)
            {
                var c = callouts[i];
                float h = KitPaint.CalloutHeight(w - 2 * Kit.Pad, c.text, c.link != null);
                string id = "callout" + i;
                KitPaint.Callout(g, new RectangleF(Kit.Pad, y, w - 2 * Kit.Pad, h), c.kind, c.text, c.link,
                    StateOf(id), out RectangleF link);
                if (c.click != null) AddTarget(id, link, c.click, null, "Open the volume check");
                y += h + Kit.Gap;
            }
            if (callouts.Count > 0) y += 4;

            // Section header with Add use on the right.
            KitPaint.Section(g, Kit.Pad, y, "Uses");
            float addW = KitPaint.ButtonWidth("Add use", KitIcon.Plus);
            var add = new RectangleF(w - Kit.Pad - addW, y, addW, Kit.ControlH);
            KitPaint.Button(g, add, "Add use", ButtonKind.Secondary, StateOf("add"), KitIcon.Plus);
            AddTarget("add", add, () => AddUse(RhinoDoc.ActiveDoc), null, "Add a new use");
            y += Kit.ControlH + Kit.Gap;

            if (_model.Categories.Count == 0)
            {
                g.DrawText(KitFonts.Body, C.Text3, Kit.Pad, y, "No uses yet. Click Add use to make one.");
            }
            else if (Pie) DrawPie(g, t, y, w);
            else DrawBars(g, t, y, w);

            DrawFooter(g, t, w);
        }

        private void DrawHeader(Graphics g, float w)
        {
            KitPaint.Header(g, w, null, GfaInfo.Name);

            RectangleF more = KitPaint.HeaderButton(w, 0);
            RectangleF help = KitPaint.HeaderButton(w, 1);
            RectangleF chart = KitPaint.HeaderButton(w, 2);

            KitPaint.Button(g, chart, null, ButtonKind.Ghost, StateOf("chart"), Pie ? KitIcon.Bars : KitIcon.Pie);
            AddTarget("chart", chart, ToggleView, null, Pie ? "Show as bars" : "Show as pie chart");

            KitPaint.Button(g, help, null, ButtonKind.Ghost, StateOf("help"), KitIcon.Help);
            AddTarget("help", help, HelpWindow.ShowWindow, null, "How to use nbGFA (F1)");

            KitPaint.Button(g, more, null, ButtonKind.Ghost, StateOf("more"), KitIcon.More);
            AddTarget("more", more, () => ShowMainMenu(this, ToControl(new PointF(more.X, more.Bottom + 2))), null, "More options");
        }

        private void DrawTiles(Graphics g, Tally t, float w, float y)
        {
            float inner = w - 2 * Kit.Pad - Kit.Gap;
            float w1 = (float)Math.Floor(inner * 0.58f);
            string mode = t.Mode == TrackingMode.ByLayer ? "by layer" : "by object";
            var r1 = new RectangleF(Kit.Pad, y, w1, TilesH);
            KitPaint.Tile(g, r1, $"Total GFA · {mode}", $"{Format(t.Total)} {t.Unit}", big: true);

            // Total GFA target: a small warning icon only when the total is off target; details on hover.
            string miss = t.TargetMiss();
            if (miss != null)
            {
                KitIcons.Draw(g, KitIcon.Warning, new RectangleF(r1.Right - 8 - 14, r1.Y + 8, 14, 14), C.Warn);
            }
            string tip = miss != null
                ? $"Off target: total GFA is {miss}. Right-click to change the target."
                : t.HasTarget
                    ? $"Total GFA is within its target ({t.TargetText()}). Right-click to change it."
                    : "Right-click to set a minimum or maximum total GFA.";
            AddTarget("total", r1, null, p => ShowTargetMenu(RhinoDoc.ActiveDoc, p), tip);

            var r2 = new RectangleF(Kit.Pad + w1 + Kit.Gap, y, inner - w1, TilesH);
            if (t.SelectedCount > 0)
                KitPaint.Tile(g, r2, "Selected", $"{Format(t.Selected)} {t.Unit}", $"{t.SelectedCount} volume(s)");
            else
                KitPaint.Tile(g, r2, "Selected", "–", "Nothing selected");
        }

        private void DrawFooter(Graphics g, Tally t, float w)
        {
            float viewport = ViewportHeight() / S;
            float top = Math.Max(BodyTop(w) + BodyHeight(w) + Kit.Pad, viewport - FooterH);
            KitPaint.HLine(g, 0, w, top, C.Border);

            float y = top + Kit.Pad;
            float lh = KitPaint.LineHeight(KitFonts.Caption);
            int broken = _model.Categories.Count(u => t.Violation(u) != null);
            bool anyLimits = _model.Categories.Any(u => u.HasLimits);
            string miss = t.TargetMiss();
            float statusW = w - 2 * Kit.Pad;
            if (broken > 0 && miss != null)
                KitPaint.StatusLine(g, Kit.Pad, y, C.Warn, $"{broken} use(s) outside their limits · total off target", statusW);
            else if (broken > 0)
                KitPaint.StatusLine(g, Kit.Pad, y, C.Warn, $"{broken} use(s) outside their limits", statusW);
            else if (miss != null)
                KitPaint.StatusLine(g, Kit.Pad, y, C.Warn, $"Total GFA is {miss}", statusW);
            else if (anyLimits || t.HasTarget)
                KitPaint.StatusLine(g, Kit.Pad, y, C.Ok, "All limits met", statusW);
            else
                KitPaint.StatusLine(g, Kit.Pad, y, C.Outline, "No limits or target set", statusW);
            y += lh + 4;

            g.DrawText(KitFonts.Caption, C.Text3, Kit.Pad, y, GfaInfo.Copyright);
        }

        // ---- bars view -----------------------------------------------------------------------

        private void DrawBars(Graphics g, Tally t, float y, float w)
        {
            foreach (UseCategory use in _model.Categories)
            {
                var rect = new RectangleF(Kit.Pad, y, w - 2 * Kit.Pad, RowH);
                string id = UseId(use);
                DrawRow(g, rect, use, t, StateOf(id));
                AddUseTarget(id, rect, use);
                y += RowH + RowGap;
            }
        }

        private static void DrawRow(Graphics g, RectangleF rect, UseCategory use, Tally t, PartState st)
        {
            KitPalette c = C;
            Color colour = Kit.ToEto(use.Color);
            double share = t.Share(use);
            string violation = t.Violation(use);

            KitPaint.Fill(g, rect, Kit.RadiusCard, st.Pressed ? c.Border : st.Hover ? c.Surface2 : c.Surface);
            if (violation != null) KitPaint.Stroke(g, rect, Kit.RadiusCard, c.Err, 1.5f);
            else KitPaint.Stroke(g, rect, Kit.RadiusCard, c.Border);

            float left = rect.X + 12;
            float right = rect.Right - 12;
            KitPaint.Fill(g, new RectangleF(left, rect.Y + 11, 10, 10), 2, colour);

            string area = $"{Format(t.Area(use))} {t.Unit}";
            float areaW = KitPaint.Measure(KitFonts.Label, area).Width;
            KitPaint.TextMiddle(g, KitFonts.Label, c.Text, left + 18, rect.Y + 6, 20,
                KitPaint.Fit(KitFonts.Label, use.Name, right - left - 18 - areaW - 8));
            KitPaint.TextMiddle(g, KitFonts.Label, c.Text, right - areaW, rect.Y + 6, 20, area);

            float line2 = rect.Y + 27;
            string detail = $"{Tally.Pct(share)} · {t.Count(use)} vol.";
            g.DrawText(KitFonts.Caption, c.Text3, left, line2, detail);
            if (violation != null)
            {
                float vw = KitPaint.Measure(KitFonts.Caption, violation).Width;
                g.DrawText(KitFonts.Caption, c.Err, right - vw, line2, violation);
                KitIcons.Draw(g, KitIcon.Warning, new RectangleF(right - vw - 16, line2, 13, 13), c.Err);
            }
            else
            {
                float detailW = KitPaint.Measure(KitFonts.Caption, detail).Width;
                string limits = KitPaint.Fit(KitFonts.Caption, LimitText(use, t.Unit), right - left - detailW - 12);
                KitPaint.TextRight(g, KitFonts.Caption, c.Text3, right, line2, limits);
            }

            KitPaint.Bar(g, new RectangleF(left, rect.Bottom - 12, right - left, 4), share, colour);
            if (st.Focused) KitPaint.FocusRing(g, rect, Kit.RadiusCard);
        }

        private static string LimitText(UseCategory use, string unit)
        {
            var parts = new List<string>();
            if (use.MinShare.HasValue && use.MaxShare.HasValue)
                parts.Add($"{Tally.Pct(use.MinShare.Value)}–{Tally.Pct(use.MaxShare.Value)}");
            else if (use.MaxShare.HasValue) parts.Add($"max {Tally.Pct(use.MaxShare.Value)}");
            else if (use.MinShare.HasValue) parts.Add($"min {Tally.Pct(use.MinShare.Value)}");

            if (use.MinArea.HasValue && use.MaxArea.HasValue)
                parts.Add($"{Tally.Num(use.MinArea.Value)}–{Tally.Num(use.MaxArea.Value)} {unit}");
            else if (use.MaxArea.HasValue) parts.Add($"max {Tally.Num(use.MaxArea.Value)} {unit}");
            else if (use.MinArea.HasValue) parts.Add($"min {Tally.Num(use.MinArea.Value)} {unit}");

            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }

        // ---- pie view ------------------------------------------------------------------------

        private sealed class PieLayout
        {
            public PointF Center;
            public float Height;
            public readonly List<(UseCategory use, float start, float sweep)> Slices = new List<(UseCategory, float, float)>();
            public readonly List<(UseCategory use, RectangleF rect, PointF rim, PointF elbow, bool right)> Labels =
                new List<(UseCategory, RectangleF, PointF, PointF, bool)>();
            public readonly List<(UseCategory use, RectangleF rect)> Chips = new List<(UseCategory, RectangleF)>();
            public float ChipsCaptionY = -1;
        }

        /// <summary>
        /// Pie centred in the window, one callout per slice on the side its slice points to (stacked so they
        /// never overlap), and chips below for uses with no GFA yet so they can still be clicked.
        /// </summary>
        private PieLayout LayoutPie(Tally t, float top, float w)
        {
            var layout = new PieLayout();
            var used = _model.Categories.Where(c => t.Area(c) > 0).ToList();
            var unused = _model.Categories.Where(c => t.Area(c) <= 0).ToList();

            float angle = -90; // start at 12 o'clock, clockwise
            var left = new List<(UseCategory use, float mid, float wantY)>();
            var right = new List<(UseCategory use, float mid, float wantY)>();
            foreach (UseCategory use in used)
            {
                float sweep = (float)(360 * t.Share(use));
                layout.Slices.Add((use, angle, sweep));
                float mid = angle + sweep / 2;
                float rad = (float)(mid * Math.PI / 180);
                float wantY = (float)Math.Sin(rad) * (PieR + 14) - LabelH / 2;
                if (Math.Cos(rad) >= 0) right.Add((use, mid, wantY)); else left.Add((use, mid, wantY));
                angle += sweep;
            }

            List<(UseCategory use, float mid, float top)> Stack(List<(UseCategory use, float mid, float wantY)> side)
            {
                var placed = new List<(UseCategory, float, float)>();
                float next = float.MinValue;
                foreach (var s in side.OrderBy(s => s.wantY))
                {
                    float y = Math.Max(s.wantY, next);
                    placed.Add((s.use, s.mid, y));
                    next = y + LabelH + LabelGap;
                }
                return placed;
            }
            var leftPlaced = Stack(left);
            var rightPlaced = Stack(right);

            float minRel = -PieR - 10;
            float maxRel = PieR + 10;
            foreach (var p in leftPlaced.Concat(rightPlaced))
            {
                minRel = Math.Min(minRel, p.Item3);
                maxRel = Math.Max(maxRel, p.Item3 + LabelH);
            }

            float cx = w / 2;
            float cy = top + 6 - minRel;
            layout.Center = new PointF(cx, cy);
            float pieBottom = cy + maxRel;

            void AddLabels(List<(UseCategory use, float mid, float top)> placed, bool isRight)
            {
                foreach (var p in placed)
                {
                    float rad = (float)(p.mid * Math.PI / 180);
                    var dir = new PointF((float)Math.Cos(rad), (float)Math.Sin(rad));
                    float x = isRight ? cx + PieR + LeaderGap : Kit.Pad;
                    float width = isRight ? w - Kit.Pad - x : cx - PieR - LeaderGap - Kit.Pad;
                    layout.Labels.Add((p.use, new RectangleF(x, cy + p.top, width, LabelH),
                        new PointF(cx + dir.X * PieR, cy + dir.Y * PieR),
                        new PointF(cx + dir.X * (PieR + 8), cy + dir.Y * (PieR + 8)),
                        isRight));
                }
            }
            AddLabels(leftPlaced, false);
            AddLabels(rightPlaced, true);

            float y2 = pieBottom + 8;
            if (unused.Count > 0)
            {
                layout.ChipsCaptionY = y2;
                y2 += KitPaint.LineHeight(KitFonts.Caption) + 4;
                float x = Kit.Pad;
                foreach (UseCategory use in unused)
                {
                    float width = Math.Min(KitPaint.ChipWidth(use.Name), w - 2 * Kit.Pad);
                    if (x + width > w - Kit.Pad && x > Kit.Pad)
                    {
                        x = Kit.Pad;
                        y2 += ChipH + 6;
                    }
                    layout.Chips.Add((use, new RectangleF(x, y2, width, ChipH)));
                    x += width + 6;
                }
                y2 += ChipH;
            }

            layout.Height = y2 + 6 - top;
            return layout;
        }

        private void DrawPie(Graphics g, Tally t, float top, float w)
        {
            KitPalette c = C;
            PieLayout layout = _pieLayout = LayoutPie(t, top, w);
            PointF centre = layout.Center;
            var circle = new RectangleF(centre.X - PieR, centre.Y - PieR, PieR * 2, PieR * 2);

            if (layout.Slices.Count == 0)
            {
                g.DrawEllipse(new Pen(c.Border, 10), RectangleF.Inflate(circle, -5, -5));
                const string empty = "No GFA yet";
                SizeF size = KitPaint.Measure(KitFonts.Caption, empty);
                g.DrawText(KitFonts.Caption, c.Text3, centre.X - size.Width / 2, centre.Y - size.Height / 2, empty);
            }

            var gap = new Pen(c.Surface, 2f);
            foreach (var s in layout.Slices)
            {
                Color fill = Kit.ToEto(s.use.Color);
                PartState st = StateOf(UseId(s.use));
                if (st.Hover) fill = Kit.Mix(fill, c.IsDark ? Colors.White : Colors.Black, 0.12f);
                g.FillPie(fill, circle, s.start, s.sweep);
                if (layout.Slices.Count > 1)
                {
                    using (IGraphicsPath slice = SlicePath(centre, circle, s.start, s.sweep)) g.DrawPath(gap, slice);
                }
            }
            foreach (var s in layout.Slices.Where(s => t.Violation(s.use) != null))
            {
                using (IGraphicsPath slice = SlicePath(centre, circle, s.start, s.sweep)) g.DrawPath(new Pen(c.Err, 2), slice);
            }

            foreach (var l in layout.Labels)
            {
                string id = UseId(l.use);
                PartState st = StateOf(id);
                string violation = t.Violation(l.use);
                RectangleF hit = RectangleF.Inflate(l.rect, 4, 0);
                if (st.Hover || st.Pressed) KitPaint.Fill(g, hit, Kit.RadiusControl, st.Pressed ? c.Border : c.Surface2);

                // Leader: rim -> elbow -> label edge.
                float labelY = l.rect.Y + 9;
                float endX = l.right ? l.rect.X - 3 : l.rect.Right + 3;
                var pen = new Pen(violation != null ? c.Err : c.Outline, 1);
                g.DrawLine(pen, l.rim, l.elbow);
                g.DrawLine(pen, l.elbow, new PointF(endX, labelY));
                g.FillEllipse(Kit.ToEto(l.use.Color), l.rim.X - 2, l.rim.Y - 2, 4, 4);

                // A broken limit replaces the figures: the words say what is wrong, not just the colour.
                string detail = violation ?? $"{Tally.Pct(t.Share(l.use))} · {Format(t.Area(l.use))} {t.Unit}";
                DrawAligned(g, KitFonts.Label, c.Text, l.rect, l.rect.Y + 1, l.use.Name, l.right);
                DrawAligned(g, KitFonts.Caption, violation != null ? c.Err : c.Text3, l.rect, l.rect.Y + 17, detail, l.right);
                if (st.Focused) KitPaint.FocusRing(g, hit, Kit.RadiusControl);
                AddUseTarget(id, hit, l.use);
            }

            if (layout.ChipsCaptionY >= 0)
            {
                g.DrawText(KitFonts.Caption, c.Text3, Kit.Pad, layout.ChipsCaptionY,
                    layout.Slices.Count == 0 ? "Uses" : "No GFA yet");
            }
            foreach (var chip in layout.Chips)
            {
                string id = UseId(chip.use);
                KitPaint.Chip(g, chip.rect, Kit.ToEto(chip.use.Color), chip.use.Name, StateOf(id));
                AddUseTarget(id, chip.rect, chip.use);
            }
        }

        private static IGraphicsPath SlicePath(PointF centre, RectangleF circle, float start, float sweep)
        {
            var path = new GraphicsPath();
            if (sweep >= 359.99f)
            {
                path.AddEllipse(circle);
                return path;
            }
            path.MoveTo(centre);
            path.AddArc(circle, start, sweep);
            path.CloseFigure();
            return path;
        }

        private static void DrawAligned(Graphics g, Font font, Color color, RectangleF rect, float y, string text, bool left)
        {
            text = KitPaint.Fit(font, text, rect.Width);
            float x = left ? rect.X : rect.Right - KitPaint.Measure(font, text).Width;
            g.DrawText(font, color, x, y, text);
        }

        /// <summary>Pie slices are not rectangles: map a point inside a slice to its use's target.</summary>
        protected override string HitExtra(PointF p)
        {
            if (!Pie || _pieLayout == null) return null;
            float dx = p.X - _pieLayout.Center.X;
            float dy = p.Y - _pieLayout.Center.Y;
            if (dx * dx + dy * dy > PieR * PieR) return null;
            float a = (float)(Math.Atan2(dy, dx) * 180 / Math.PI);
            if (a < -90) a += 360; // slices run from -90 to 270
            foreach (var s in _pieLayout.Slices)
            {
                if (a >= s.start && a < s.start + s.sweep) return UseId(s.use);
            }
            return _pieLayout.Slices.Count == 1 ? UseId(_pieLayout.Slices[0].use) : null;
        }

        // ---- interaction ---------------------------------------------------------------------

        private static string UseId(UseCategory use) => "use:" + use.Name;

        private void AddUseTarget(string id, RectangleF rect, UseCategory use) =>
            AddTarget(id, rect,
                () => Assign(RhinoDoc.ActiveDoc, use, SelectedVolumes(RhinoDoc.ActiveDoc)),
                p => ShowMenu(RhinoDoc.ActiveDoc, use, p),
                $"{use.Name}: click to assign the selected volumes, right-click for options");

        private static List<RhinoObject> SelectedVolumes(RhinoDoc doc) =>
            doc == null ? new List<RhinoObject>()
                        : doc.Objects.GetSelectedObjects(false, false).Where(Volumes.IsVolume).ToList();

        private void Assign(RhinoDoc doc, UseCategory use, List<RhinoObject> volumes)
        {
            if (doc == null) return;
            if (volumes.Count == 0)
            {
                RhinoApp.WriteLine($"{GfaInfo.Name}: select volumes first, then click {use.Name}.");
                return;
            }
            int n = _model.Assign(doc, volumes, use);
            RhinoApp.WriteLine($"{GfaInfo.Name}: {n} volume(s) set to {use.Name}.");
        }

        private void ShowMenu(RhinoDoc doc, UseCategory use, PointF location)
        {
            if (doc == null) return;
            // Capture the selection now, while the menu opens, rather than when an item is clicked.
            List<RhinoObject> selected = SelectedVolumes(doc);
            List<RhinoObject> selectedAssigned = _model.WithAnyUse(doc, selected);
            List<RhinoObject> ofThisUse = _model.ObjectsWithUse(doc, use);

            var menu = new ContextMenu();

            menu.Items.Add(Item($"Assign {selected.Count} selected to {use.Name}", selected.Count > 0,
                () => Assign(doc, use, selected)));
            menu.Items.Add(Item($"Exclude {selectedAssigned.Count} selected from GFA", selectedAssigned.Count > 0, () =>
            {
                int n = _model.Exclude(doc, selectedAssigned);
                RhinoApp.WriteLine($"{GfaInfo.Name}: {n} volume(s) excluded from GFA.");
            }));
            menu.Items.Add(Item($"Select all {use.Name} ({ofThisUse.Count})", ofThisUse.Count > 0, () =>
            {
                doc.Objects.UnselectAll();
                foreach (RhinoObject o in ofThisUse) o.Select(true);
                doc.Views.Redraw();
            }));
            menu.Items.Add(Item($"Exclude all {use.Name} ({ofThisUse.Count}) from GFA", ofThisUse.Count > 0, () =>
            {
                int n = _model.Exclude(doc, ofThisUse);
                RhinoApp.WriteLine($"{GfaInfo.Name}: {n} {use.Name} volume(s) excluded from GFA.");
            }));

            menu.Items.Add(new SeparatorMenuItem());
            string unit = _model.Current.Unit;
            menu.Items.Add(Item("Set max %…", true, () => EditShareLimit(doc, use, max: true)));
            menu.Items.Add(Item("Set min %…", true, () => EditShareLimit(doc, use, max: false)));
            menu.Items.Add(Item($"Set max {unit}…", true, () => EditAreaLimit(doc, use, max: true)));
            menu.Items.Add(Item($"Set min {unit}…", true, () => EditAreaLimit(doc, use, max: false)));
            if (use.HasLimits)
            {
                menu.Items.Add(Item("Clear limits", true, () =>
                {
                    use.ClearLimits();
                    _model.SaveCategories(doc);
                }));
            }

            menu.Items.Add(new SeparatorMenuItem());
            menu.Items.Add(Item("Rename / colour…", true, () => EditUse(doc, use)));
            menu.Items.Add(Item($"Remove {use.Name}…", true, () => RemoveUse(doc, use)));
            menu.Items.Add(Item("Add use…", true, () => AddUse(doc)));

            menu.Show(this, ToControl(location));
        }

        /// <summary>The window's ⋯ menu, opened from the title bar at a point in <paramref name="owner"/>'s pixels.</summary>
        public void ShowMainMenu(Control owner, PointF location)
        {
            RhinoDoc doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            var menu = new ContextMenu();

            menu.Items.Add(Item("How to use nbGFA", true, HelpWindow.ShowWindow));
            menu.Items.Add(new SeparatorMenuItem());

            var byObject = new RadioMenuItem { Text = "Track uses by object", Checked = _model.Mode == TrackingMode.ByObject };
            var byLayer = new RadioMenuItem(byObject) { Text = "Track uses by layer (nbGFA::<use>)", Checked = _model.Mode == TrackingMode.ByLayer };
            byObject.Click += (s, e) => SwitchMode(doc, TrackingMode.ByObject);
            byLayer.Click += (s, e) => SwitchMode(doc, TrackingMode.ByLayer);
            menu.Items.Add(byObject);
            menu.Items.Add(byLayer);

            menu.Items.Add(new SeparatorMenuItem());
            menu.Items.Add(Item(Pie ? "Show as bars" : "Show as pie chart", true, ToggleView));
            menu.Items.Add(Item("Check volumes…", true, () => IssuesDialog.Show(ParentWindow, _model, doc)));
            menu.Items.Add(Item("Add use…", true, () => AddUse(doc)));
            var target = new SubMenuItem { Text = "Total GFA target" };
            foreach (MenuItem item in TargetItems(doc)) target.Items.Add(item);
            menu.Items.Add(target);

            menu.Items.Add(new SeparatorMenuItem());
            Display.AddMenuItems(menu, () => DisplayChanged?.Invoke(this, EventArgs.Empty));

            menu.Show(owner, location);
        }

        /// <summary>Raised when interface size or outlines change, so the window can resize.</summary>
        public event EventHandler DisplayChanged;

        private void SwitchMode(RhinoDoc doc, TrackingMode mode)
        {
            if (mode == _model.Mode) return;
            string message = mode == TrackingMode.ByLayer
                ? "Track uses by layer?\n\nnbGFA creates an \"nbGFA\" layer with one sublayer per use and moves assigned " +
                  "volumes onto them, remembering the layer each came from. From then on, moving a volume onto a " +
                  "use's layer assigns it."
                : "Track uses by object?\n\nVolumes go back to the layers they came from; their use is stored on the " +
                  "object and shown by object colour.";
            if (MessageBox.Show(this, message, GfaInfo.Name, MessageBoxButtons.OKCancel, MessageBoxType.Question)
                != DialogResult.Ok) return;

            _model.SetMode(doc, mode);
            RhinoApp.WriteLine($"{GfaInfo.Name}: now tracking uses by {(mode == TrackingMode.ByLayer ? "layer" : "object")}.");
        }

        private void EditShareLimit(RhinoDoc doc, UseCategory use, bool max)
        {
            double? current = max ? use.MaxShare : use.MinShare;
            LimitDialog.Answer answer = LimitDialog.Ask(ParentWindow, $"{(max ? "Max" : "Min")} share for {use.Name}",
                "Share", "% of total GFA", current * 100, 100);
            if (answer == null) return;
            double? value = answer.Value / 100;
            if (max) use.MaxShare = value; else use.MinShare = value;
            _model.SaveCategories(doc);
        }

        private void EditAreaLimit(RhinoDoc doc, UseCategory use, bool max)
        {
            string unit = _model.Current.Unit;
            double? current = max ? use.MaxArea : use.MinArea;
            LimitDialog.Answer answer = LimitDialog.Ask(ParentWindow, $"{(max ? "Max" : "Min")} GFA for {use.Name}",
                "GFA", unit, current, null);
            if (answer == null) return;
            if (max) use.MaxArea = answer.Value; else use.MinArea = answer.Value;
            _model.SaveCategories(doc);
        }

        private IEnumerable<MenuItem> TargetItems(RhinoDoc doc)
        {
            string unit = _model.Current.Unit;
            yield return Item($"Set max total {unit}…", true, () => EditTarget(doc, max: true));
            yield return Item($"Set min total {unit}…", true, () => EditTarget(doc, max: false));
            yield return Item("Clear target", _model.TargetMin.HasValue || _model.TargetMax.HasValue,
                () => _model.SetTarget(doc, null, null));
        }

        private void ShowTargetMenu(RhinoDoc doc, PointF location)
        {
            if (doc == null) return;
            var menu = new ContextMenu();
            foreach (MenuItem item in TargetItems(doc)) menu.Items.Add(item);
            menu.Show(this, ToControl(location));
        }

        private void EditTarget(RhinoDoc doc, bool max)
        {
            if (doc == null) return;
            string unit = _model.Current.Unit;
            double? current = max ? _model.TargetMax : _model.TargetMin;
            LimitDialog.Answer answer = LimitDialog.Ask(ParentWindow, $"{(max ? "Max" : "Min")} total GFA",
                "Total GFA", unit, current, null);
            if (answer == null) return;
            if (max) _model.SetTarget(doc, _model.TargetMin, answer.Value);
            else _model.SetTarget(doc, answer.Value, _model.TargetMax);
        }

        private void AddUse(RhinoDoc doc)
        {
            if (doc == null) return;
            SdColor suggestion = UseCategory.FallbackColor(Guid.NewGuid().ToString());
            var result = UseDialog.Ask(ParentWindow, "Add use", string.Empty, suggestion, n => _model.ValidateName(n), "Add use");
            if (result == null) return;
            _model.AddCategory(doc, result.Value.name, result.Value.color);
        }

        private void EditUse(RhinoDoc doc, UseCategory use)
        {
            var result = UseDialog.Ask(ParentWindow, $"Edit {use.Name}", use.Name, use.Color, n => _model.ValidateName(n, use), "Save");
            if (result == null) return;
            if (!string.Equals(result.Value.name, use.Name, StringComparison.Ordinal))
            {
                _model.RenameCategory(doc, use, result.Value.name);
            }
            if (result.Value.color.ToArgb() != use.Color.ToArgb())
            {
                _model.RecolorCategory(doc, use, result.Value.color);
            }
        }

        private void RemoveUse(RhinoDoc doc, UseCategory use)
        {
            int count = _model.ObjectsWithUse(doc, use).Count();
            UseCategory fallback = _model.FallbackFor(use);
            string message = count == 0
                ? $"Remove the use \"{use.Name}\"?"
                : fallback != null
                    ? $"Remove the use \"{use.Name}\"?\n\n{count} volume(s) will move to \"{fallback.Name}\" and stay in the total."
                    : $"Remove the use \"{use.Name}\"?\n\n{count} volume(s) will be excluded from the total and go back to their layer colour.";
            if (MessageBox.Show(this, message, GfaInfo.Name, MessageBoxButtons.YesNo, MessageBoxType.Question)
                != DialogResult.Yes) return;

            int n = _model.RemoveCategory(doc, use);
            RhinoApp.WriteLine(fallback != null
                ? $"{GfaInfo.Name}: removed {use.Name}; {n} volume(s) moved to {fallback.Name}."
                : $"{GfaInfo.Name}: removed {use.Name}; {n} volume(s) excluded from GFA.");
        }

        private static ButtonMenuItem Item(string text, bool enabled, Action action)
        {
            var item = new ButtonMenuItem { Text = text, Enabled = enabled };
            item.Click += (s, e) => action();
            return item;
        }

        private static string Format(double area) => area.ToString("#,0");
    }
}
