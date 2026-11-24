using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;

namespace Numbat.UI
{
    /// <summary>
    /// Base for every custom-drawn part (the design's "AccessibleDrawable"). Subclasses paint in design px and
    /// register clickable targets while painting; the base handles the rest:
    /// <list type="bullet">
    /// <item>interface-size scaling (paint and mouse),</item>
    /// <item>hover, pressed and keyboard-focus state per target, with the focus ring,</item>
    /// <item>keyboard: Tab / Shift+Tab and arrows move between targets, Space / Enter click,
    /// Shift+F10 or the menu key opens a target's context menu, F1 raises <see cref="HelpRequested"/>,</item>
    /// <item>tooltips and the hand cursor over targets.</item>
    /// </list>
    /// Screen readers can't see inside a Drawable, so give each target a tooltip and keep its text visible.
    /// </summary>
    internal abstract class KitCanvas : Drawable
    {
        protected sealed class Target
        {
            public string Id;
            public RectangleF Rect;
            public Action Click;
            public Action<PointF> Context;
            public string Tip;
            public bool Enabled = true;
        }

        private readonly List<Target> _targets = new List<Target>();
        private string _hover, _pressed, _focus;
        private UITimer _tipTimer;
        private bool _tipShown;
        private bool _keyboard; // focus ring only after keyboard use, like :focus-visible

        /// <summary>F1 was pressed while this part had focus.</summary>
        public event EventHandler HelpRequested;

        protected KitCanvas()
        {
            CanFocus = true;
            Paint += (s, e) => PaintAll(e.Graphics);
            MouseMove += OnMove;
            MouseLeave += (s, e) => SetHover(null);
            MouseDown += OnDown;
            MouseUp += OnUp;
            KeyDown += OnKey;
            GotFocus += (s, e) =>
            {
                // Small parts (buttons, switches) are usually reached by Tab, so they show the ring on focus.
                // Large canvases also get focus when their window opens; they show it only after a key press.
                _keyboard = RingOnFocus;
                if (_focus == null) _focus = _targets.FirstOrDefault(t => t.Enabled)?.Id;
                Invalidate();
            };
            LostFocus += (s, e) => { _pressed = null; Invalidate(); };
        }

        /// <summary>
        /// Draw hover tips inside the canvas instead of using the native tooltip. Use for large canvases with many
        /// targets: the native tooltip of a single control is only read when the mouse enters it, so per-target tips
        /// would never show. Small single controls keep the native tooltip, which can extend past their bounds.
        /// </summary>
        protected virtual bool DrawnTips => false;

        /// <summary>Show the focus ring as soon as the part gets focus (true for single controls).</summary>
        protected virtual bool RingOnFocus => false;

        /// <summary>Interface size multiplier.</summary>
        protected static float S => Kit.Scale;

        /// <summary>Width available in design px.</summary>
        protected float LogicalWidth => Width / S;

        /// <summary>Height available in design px.</summary>
        protected float LogicalHeight => Height / S;

        /// <summary>Background behind everything; defaults to the surface colour.</summary>
        protected virtual Color CanvasBackground => Kit.Colours.Surface;

        protected abstract void PaintContent(Graphics g);

        private void PaintAll(Graphics g)
        {
            g.AntiAlias = true;
            g.FillRectangle(CanvasBackground, 0, 0, Width, Height);
            g.SaveTransform();
            g.ScaleTransform(S);
            _targets.Clear();
            PaintContent(g);
            if (DrawnTips && _tipShown) PaintTip(g);
            g.RestoreTransform();
            if (_focus != null && Find(_focus) == null) _focus = null;
        }

        // ---- targets ----------------------------------------------------------------------------

        /// <summary>Registers a clickable target for this frame. Call while painting, in reading order.</summary>
        protected void AddTarget(string id, RectangleF rect, Action click, Action<PointF> context = null,
            string tip = null, bool enabled = true)
        {
            _targets.Add(new Target { Id = id, Rect = rect, Click = click, Context = context, Tip = tip, Enabled = enabled });
        }

        /// <summary>State to draw a target with, from the previous frame's input.</summary>
        protected PartState StateOf(string id, bool enabled = true) => new PartState
        {
            Hover = enabled && id == _hover,
            Pressed = enabled && id == _pressed && id == _hover,
            Focused = _keyboard && HasFocus && id == _focus,
            Disabled = !enabled,
        };

        /// <summary>Extra hit test for non-rectangular parts (pie slices): return a target id or null.</summary>
        protected virtual string HitExtra(PointF p) => null;

        protected Target Find(string id) => id == null ? null : _targets.FirstOrDefault(t => t.Id == id);

        private Target HitTest(PointF p)
        {
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                if (_targets[i].Rect.Contains(p)) return _targets[i];
            }
            return Find(HitExtra(p));
        }

        /// <summary>Converts a point in design px to control pixels (for menus and popups).</summary>
        protected PointF ToControl(PointF p) => new PointF(p.X * S, p.Y * S);

        private PointF ToLogical(PointF p) => new PointF(p.X / S, p.Y / S);

        // ---- mouse ------------------------------------------------------------------------------

        private void SetHover(Target t)
        {
            string id = t != null && t.Enabled ? t.Id : null;
            if (id == _hover) return;
            _hover = id;
            if (DrawnTips) RestartTip(t?.Tip);
            else ToolTip = t?.Tip;
            Cursor = id != null ? Cursors.Pointer : Cursors.Default;
            Invalidate();
        }

        // ---- drawn tips ---------------------------------------------------------------------------

        private void RestartTip(string tip)
        {
            _tipTimer?.Stop();
            bool wasShown = _tipShown;
            _tipShown = false;
            if (wasShown) Invalidate();
            if (string.IsNullOrEmpty(tip)) return;
            if (_tipTimer == null)
            {
                _tipTimer = new UITimer { Interval = 0.45 };
                _tipTimer.Elapsed += (s, e) =>
                {
                    _tipTimer.Stop();
                    _tipShown = true;
                    Invalidate();
                };
            }
            _tipTimer.Start();
        }

        /// <summary>The hovered target's tip as a small dark box just below it (or above, near the bottom).</summary>
        private void PaintTip(Graphics g)
        {
            Target t = Find(_hover);
            if (t == null || string.IsNullOrEmpty(t.Tip)) return;
            KitPalette c = Kit.Colours;
            Font font = KitFonts.Caption;
            float maxText = Math.Min(260, LogicalWidth - 32);
            List<string> lines = KitPaint.Wrap(font, t.Tip, maxText);
            float lh = KitPaint.LineHeight(font);
            float textW = 0;
            foreach (string line in lines) textW = Math.Max(textW, KitPaint.Measure(font, line).Width);
            float w = textW + 16, h = lines.Count * lh + 10;
            float x = Math.Max(8, Math.Min(t.Rect.X, LogicalWidth - w - 8));
            float y = t.Rect.Bottom + 6;
            if (y + h > LogicalHeight - 4) y = Math.Max(4, t.Rect.Y - h - 6);
            var box = new RectangleF(x, y, w, h);
            KitPaint.Fill(g, box, 6, c.Text);
            for (int i = 0; i < lines.Count; i++) g.DrawText(font, c.Surface, x + 8, y + 5 + i * lh, lines[i]);
        }

        private void OnMove(object sender, MouseEventArgs e) => SetHover(HitTest(ToLogical(e.Location)));

        private void OnDown(object sender, MouseEventArgs e)
        {
            Focus();
            _keyboard = false;
            PointF p = ToLogical(e.Location);
            Target t = HitTest(p);
            if (t == null || !t.Enabled)
            {
                if (t == null) OnEmptyMouseDown(e);
                return;
            }
            e.Handled = true;
            _focus = t.Id;
            if (e.Buttons == MouseButtons.Primary)
            {
                _pressed = t.Id;
                Invalidate();
            }
            else if (e.Buttons == MouseButtons.Alternate && t.Context != null)
            {
                t.Context(p);
            }
        }

        private void OnUp(object sender, MouseEventArgs e)
        {
            if (_pressed == null) return;
            string pressed = _pressed;
            _pressed = null;
            Target t = HitTest(ToLogical(e.Location));
            Invalidate();
            if (t != null && t.Id == pressed && t.Enabled)
            {
                e.Handled = true;
                t.Click?.Invoke();
            }
        }

        /// <summary>Mouse pressed where there is no target (the title bar uses this to move its window).</summary>
        protected virtual void OnEmptyMouseDown(MouseEventArgs e) { }

        // ---- keyboard ---------------------------------------------------------------------------

        private void OnKey(object sender, KeyEventArgs e)
        {
            List<Target> enabled = _targets.Where(t => t.Enabled).ToList();
            int index = enabled.FindIndex(t => t.Id == _focus);
            bool shift = e.Modifiers.HasFlag(Keys.Shift);

            switch (e.Key)
            {
                case Keys.Tab:
                    int next = index < 0 ? (shift ? enabled.Count - 1 : 0) : index + (shift ? -1 : 1);
                    if (next < 0 || next >= enabled.Count) return; // leave the canvas: let Tab move on
                    MoveFocus(enabled[next].Id, e);
                    break;
                case Keys.Down:
                case Keys.Right:
                    if (enabled.Count > 0) MoveFocus(enabled[Math.Min(enabled.Count - 1, index + 1)].Id, e);
                    break;
                case Keys.Up:
                case Keys.Left:
                    if (enabled.Count > 0) MoveFocus(enabled[Math.Max(0, index - 1)].Id, e);
                    break;
                case Keys.Home:
                    if (enabled.Count > 0) MoveFocus(enabled[0].Id, e);
                    break;
                case Keys.End:
                    if (enabled.Count > 0) MoveFocus(enabled[enabled.Count - 1].Id, e);
                    break;
                case Keys.Space:
                case Keys.Enter:
                    if (index >= 0)
                    {
                        e.Handled = true;
                        enabled[index].Click?.Invoke();
                    }
                    break;
                case Keys.F10 when shift:
                case Keys.ContextMenu:
                    if (index >= 0 && enabled[index].Context != null)
                    {
                        e.Handled = true;
                        RectangleF r = enabled[index].Rect;
                        enabled[index].Context(new PointF(r.X + 8, r.Bottom));
                    }
                    break;
                case Keys.F1:
                    e.Handled = true;
                    HelpRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        private void MoveFocus(string id, KeyEventArgs e)
        {
            e.Handled = true;
            _keyboard = true;
            _focus = id;
            Invalidate();
        }

        /// <summary>The focused target's rectangle in design px, or null (for scrolling it into view).</summary>
        protected RectangleF? FocusedRect => Find(_focus)?.Rect;
    }
}
