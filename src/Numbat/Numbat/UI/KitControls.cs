using System;
using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;

namespace Numbat.UI
{
    /// <summary>
    /// Custom-drawn button for Eto layouts: primary, secondary, ghost or danger; optional icon, count badge and
    /// toggled ("On") state. Sizes itself to its content at 28 px high; stretch it in a layout for full width.
    /// </summary>
    internal sealed class KitButton : KitCanvas
    {
        protected override bool RingOnFocus => true;

        private string _text;
        private KitIcon _icon;
        private string _badge;
        private bool _on;
        private ButtonKind _kind;

        public event EventHandler Click;

        public KitButton(string text, ButtonKind kind = ButtonKind.Secondary, KitIcon icon = KitIcon.None)
        {
            _text = text;
            _kind = kind;
            _icon = icon;
            if (string.IsNullOrEmpty(text)) ToolTip = icon.ToString();
            UpdateSize();
        }

        public string Text { get => _text; set { _text = value; UpdateSize(); } }
        public KitIcon Icon { get => _icon; set { _icon = value; UpdateSize(); } }
        public ButtonKind Kind { get => _kind; set { _kind = value; Invalidate(); } }

        /// <summary>Count shown in a badge (pick buttons), or null.</summary>
        public string Badge { get => _badge; set { _badge = value; UpdateSize(); } }

        /// <summary>Toggled / picking state: accent outline on accent-weak.</summary>
        public bool On { get => _on; set { _on = value; Invalidate(); } }

        /// <summary>Content left-aligned with the badge at the right edge (pick buttons).</summary>
        public bool AlignLeft { get; set; }

        /// <summary>Tooltip, which is also what an icon-only button is called.</summary>
        public string Tip { get; set; }

        public void PerformClick()
        {
            if (Enabled) Click?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateSize()
        {
            // +2 px slack: the button is drawn inset by half a pixel and text measurement rounds, so a button sized
            // to the exact text width would clip its last letters ("Check aga…").
            float slack = string.IsNullOrEmpty(_text) ? 0 : 2;
            Size = new Size((int)Math.Ceiling((KitPaint.ButtonWidth(_text, _icon, _badge) + slack) * S),
                (int)Math.Ceiling(Kit.ControlH * S));
            Invalidate();
        }

        protected override Color CanvasBackground => BackgroundColor.A > 0 ? BackgroundColor : Kit.Colours.Surface;

        protected override void PaintContent(Graphics g)
        {
            var r = new RectangleF(0, 0, LogicalWidth, Kit.ControlH);
            r.Y = (LogicalHeight - Kit.ControlH) / 2;
            AddTarget("button", r, PerformClick, null, Tip ?? (string.IsNullOrEmpty(_text) ? _icon.ToString() : null), Enabled);
            PartState state = StateOf("button", Enabled);
            state.On = _on;
            KitPaint.Button(g, RectangleF.Inflate(r, -0.5f, -0.5f), _text, _kind, state, _icon, _badge, AlignLeft);
        }
    }

    /// <summary>A kit icon as a control, for native layouts (e.g. a warning icon beside a heading). Not focusable.</summary>
    internal sealed class KitIconView : Drawable
    {
        public KitIconView(KitIcon icon, Func<KitPalette, Color> colour = null, float size = 16)
        {
            int px = (int)Math.Ceiling(size * Kit.Scale);
            Size = new Size(px, px);
            Paint += (s, e) =>
            {
                e.Graphics.AntiAlias = true;
                KitPalette c = Kit.Colours;
                KitIcons.Draw(e.Graphics, icon, new RectangleF(0, 0, px, px), colour != null ? colour(c) : c.Text2);
            };
        }
    }

    /// <summary>Segmented picker (e.g. Quad / Tri / Hex). Arrow keys move the selection.</summary>
    internal sealed class KitSegmented : KitCanvas
    {
        protected override bool RingOnFocus => true;

        private readonly List<string> _options;
        private int _selected;

        public event EventHandler SelectedIndexChanged;

        public KitSegmented(IEnumerable<string> options, int selected = 0)
        {
            _options = new List<string>(options);
            _selected = selected;
            float w = 4;
            foreach (string o in _options) w += KitPaint.Measure(KitFonts.Label, o).Width + 20 + 2;
            Size = new Size((int)Math.Ceiling(w * S), (int)Math.Ceiling(Kit.ControlH * S));
        }

        public int SelectedIndex
        {
            get => _selected;
            set
            {
                if (value == _selected || value < 0 || value >= _options.Count) return;
                _selected = value;
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void PaintContent(Graphics g)
        {
            var r = new RectangleF(0.5f, (LogicalHeight - Kit.ControlH) / 2 + 0.5f, LogicalWidth - 1, Kit.ControlH - 1);
            int hover = -1, focused = -1;
            for (int i = 0; i < _options.Count; i++)
            {
                PartState st = StateOf("seg" + i, Enabled);
                if (st.Hover) hover = i;
                if (st.Focused) focused = i;
            }
            RectangleF[] rects = KitPaint.Segmented(g, r, _options, _selected, hover, focused, !Enabled);
            for (int i = 0; i < rects.Length; i++)
            {
                int index = i;
                AddTarget("seg" + i, rects[i], () => SelectedIndex = index, null, _options[i], Enabled);
            }
        }
    }

    /// <summary>On/off switch with its label drawn beside it.</summary>
    internal sealed class KitSwitch : KitCanvas
    {
        protected override bool RingOnFocus => true;

        private bool _checked;
        private readonly string _label;

        public event EventHandler CheckedChanged;

        public KitSwitch(string label, bool isChecked = false)
        {
            _label = label;
            _checked = isChecked;
            float w = 28 + (string.IsNullOrEmpty(label) ? 0 : 8 + KitPaint.Measure(KitFonts.Body, label).Width);
            Size = new Size((int)Math.Ceiling((w + 4) * S), (int)Math.Ceiling(Kit.ControlH * S));
        }

        public bool Checked
        {
            get => _checked;
            set
            {
                if (value == _checked) return;
                _checked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void PaintContent(Graphics g)
        {
            float top = (LogicalHeight - 16) / 2;
            var all = new RectangleF(0, 0, LogicalWidth, LogicalHeight);
            AddTarget("switch", all, () => Checked = !Checked, null, _label, Enabled);
            PartState st = StateOf("switch", Enabled);
            KitPaint.Switch(g, new PointF(2, top), _checked, st);
            if (!string.IsNullOrEmpty(_label))
            {
                KitPalette c = Kit.Colours;
                KitPaint.TextMiddle(g, KitFonts.Body, Enabled ? c.Text : c.Text3, 2 + 28 + 8, 0, LogicalHeight, _label);
            }
        }
    }
}
