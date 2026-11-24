using System;
using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;

namespace Numbat.UI
{
    /// <summary>
    /// The kit header band (<see cref="KitPaint.Header"/>) as a control, for windows built from native Eto controls.
    /// Shows the command name with icon buttons on the right; add them in the standard order, right to left:
    /// ⋯ (menu), ? (guide), then view toggles. Custom-drawn windows paint the header themselves instead.
    /// </summary>
    internal sealed class KitHeaderBar : KitCanvas
    {
        private sealed class HeaderButton
        {
            public KitIcon Icon;
            public string Tip;
            public Action<KitHeaderBar, PointF> Click;
        }

        private readonly string _title;
        private readonly List<HeaderButton> _buttons = new List<HeaderButton>();

        public KitHeaderBar(string title)
        {
            _title = title;
            Height = (int)Math.Ceiling(Kit.HeaderH * S);
        }

        /// <summary>
        /// Adds the next button from the right. <paramref name="click"/> gets this bar and the point (in this bar's
        /// pixels) just below the button's left edge, where a menu should open.
        /// </summary>
        public KitHeaderBar Add(KitIcon icon, string tip, Action<KitHeaderBar, PointF> click)
        {
            _buttons.Add(new HeaderButton { Icon = icon, Tip = tip, Click = click });
            Invalidate();
            return this;
        }

        /// <summary>Adds the next button from the right with a plain click action.</summary>
        public KitHeaderBar Add(KitIcon icon, string tip, Action click) => Add(icon, tip, (bar, p) => click());

        /// <summary>Re-reads the interface size (after the display options change).</summary>
        public void UpdateSize()
        {
            Height = (int)Math.Ceiling(Kit.HeaderH * S);
            Invalidate();
        }

        protected override void PaintContent(Graphics g)
        {
            float w = LogicalWidth;
            KitPaint.Header(g, w, null, _title);
            for (int i = 0; i < _buttons.Count; i++)
            {
                HeaderButton b = _buttons[i];
                RectangleF r = KitPaint.HeaderButton(w, i);
                string id = "h" + i;
                KitPaint.Button(g, r, null, ButtonKind.Ghost, StateOf(id), b.Icon);
                AddTarget(id, r, () => b.Click(this, ToControl(new PointF(r.X, r.Bottom + 2))), null, b.Tip);
            }
        }
    }

    /// <summary>
    /// Read-only text columns for a native <see cref="GridView"/> that never make the grid wider than its space:
    /// columns keep their width, text that doesn't fit ends in "…", and hovering a shortened cell shows the full text
    /// in a <see cref="KitTip"/>. Create one per grid.
    /// </summary>
    internal sealed class KitGridText
    {
        private const float CellPad = 4;
        private readonly GridView _grid;
        private readonly Dictionary<GridColumn, Func<object, string>> _texts = new Dictionary<GridColumn, Func<object, string>>();
        private readonly KitTip _tip = new KitTip();

        public KitGridText(GridView grid)
        {
            _grid = grid;
            grid.BackgroundColor = Kit.Colours.Surface; // the empty rows below the list, to match the cells
            grid.GridLines = GridLines.None; // native lines keep the native colours; the cells' own backgrounds join up
            grid.MouseMove += (s, e) => ShowTip(e.Location);
            grid.MouseLeave += (s, e) => _tip.Hide();
            grid.MouseDown += (s, e) => _tip.Hide();
            grid.UnLoad += (s, e) => _tip.Close();
        }

        private static Font CellFont => KitFonts.Scaled(12, Weight.Regular);

        /// <summary>
        /// Adds a text column. <paramref name="width"/> is in design px; <paramref name="expand"/> columns share the
        /// width left over. <paramref name="secondary"/> draws a cell's text in the secondary colour.
        /// </summary>
        public GridColumn Add<T>(string header, float width, Func<T, string> text, bool expand = false, Func<T, bool> secondary = null)
        {
            var cell = new DrawableCell();
            cell.Paint += (s, e) =>
            {
                if (!(e.Item is T item)) return;
                KitPalette c = Kit.Colours;
                RectangleF r = e.ClipRectangle;
                // The cell paints its own kit background, so its text reads in light and dark mode whatever colours
                // the native grid uses. A selected row gets the accent tint, like the kit's toggled buttons.
                e.Graphics.FillRectangle(e.IsSelected ? c.AccentWeak : c.Surface, r);
                Color ink = secondary != null && secondary(item) ? c.Text3 : c.Text;
                string shown = KitPaint.Fit(CellFont, OneLine(text(item)), r.Width - 2 * CellPad);
                KitPaint.TextMiddle(e.Graphics, CellFont, ink, r.X + CellPad, r.Y, r.Height, shown);
            };
            var column = new GridColumn
            {
                HeaderText = header,
                Width = (int)(width * Kit.Scale),
                AutoSize = false, // an auto-sized column grows with its longest text and widens the grid
                Expand = expand,
                Editable = false,
                DataCell = cell,
            };
            _texts[column] = item => item is T t ? OneLine(text(t)) : null;
            _grid.Columns.Add(column);
            return column;
        }

        private static string OneLine(string s) => (s ?? string.Empty).Replace("\r", " ").Replace("\n", " ");

        private void ShowTip(PointF location)
        {
            GridCell cell = _grid.GetCellAt(location);
            string full = null;
            if (cell?.Column != null && cell.Item != null && _texts.TryGetValue(cell.Column, out Func<object, string> text))
            {
                full = text(cell.Item);
                // Only shortened cells need the tip.
                if (KitPaint.Fit(CellFont, full, cell.Column.Width - 2 * CellPad) == full) full = null;
            }
            _tip.Show(full, _grid.ParentWindow);
        }
    }

    /// <summary>
    /// A hover tip for native controls, drawn like the tips inside kit canvases (a small dark box), in a borderless
    /// window that doesn't take focus. Use it where the native tooltip can't follow the mouse, for example from cell to
    /// cell in a grid: the native one is only looked up when the mouse enters a control. Shows after a short delay,
    /// just below and right of the mouse.
    /// </summary>
    internal sealed class KitTip
    {
        private const float MaxWidth = 360;
        private Form _form;
        private UITimer _timer;
        private string _text;
        private Window _owner;

        private static Font TipFont => KitFonts.Scaled(11, Weight.Regular);
        private static float TipPad => 8 * Kit.Scale;

        /// <summary>Shows <paramref name="text"/> after a short delay (or switches to it at once if a tip is open). Null hides it.</summary>
        public void Show(string text, Window owner)
        {
            if (text == _text) return;
            bool wasOpen = _form != null && _form.Visible;
            Hide();
            _text = text;
            _owner = owner;
            if (text == null) return;
            if (wasOpen)
            {
                Open();
                return;
            }
            if (_timer == null)
            {
                _timer = new UITimer { Interval = 0.45 };
                _timer.Elapsed += (s, e) =>
                {
                    _timer.Stop();
                    Open();
                };
            }
            _timer.Start();
        }

        public void Hide()
        {
            _timer?.Stop();
            _text = null;
            if (_form != null && _form.Visible) _form.Visible = false;
        }

        /// <summary>Hides the tip and frees its window (when the control goes away).</summary>
        public void Close()
        {
            Hide();
            _form?.Close();
            _form = null;
        }

        private void Open()
        {
            if (_text == null) return;
            if (_form == null)
            {
                var box = new Drawable();
                box.Paint += (s, e) => Paint(e.Graphics, box.Size);
                // The tip never sits under the mouse: if the mouse reaches it, it goes, and comes back beside the
                // mouse after the usual delay.
                box.MouseEnter += (s, e) => Hide();
                _form = new Form
                {
                    WindowStyle = WindowStyle.None,
                    Resizable = false,
                    Minimizable = false,
                    Maximizable = false,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Topmost = true,
                    Content = box,
                };
            }
            List<string> lines = KitPaint.Wrap(TipFont, _text, MaxWidth * Kit.Scale);
            float textW = 0;
            foreach (string line in lines) textW = Math.Max(textW, KitPaint.Measure(TipFont, line).Width);
            var size = new Size((int)Math.Ceiling(textW + 2 * TipPad), (int)Math.Ceiling(lines.Count * KitPaint.LineHeight(TipFont) + 1.25f * TipPad));

            // Below right of the mouse, kept on its screen.
            PointF mouse = Mouse.Position;
            RectangleF area = (Screen.FromPoint(mouse) ?? Screen.PrimaryScreen).WorkingArea;
            float x = Math.Min(mouse.X + 12, area.Right - size.Width - 4);
            float y = mouse.Y + 20;
            if (y + size.Height > area.Bottom - 4) y = mouse.Y - size.Height - 8;
            if (_owner != null && _form.Owner != _owner) _form.Owner = _owner;
            _form.ClientSize = size;
            _form.Location = new Point((int)Math.Max(area.X + 4, x), (int)y);
            _form.Content.Invalidate();
            if (!_form.Visible) _form.Show();
        }

        private void Paint(Graphics g, Size size)
        {
            KitPalette c = Kit.Colours;
            g.FillRectangle(c.Text, 0, 0, size.Width, size.Height);
            if (_text == null) return;
            float lh = KitPaint.LineHeight(TipFont);
            float y = TipPad * 0.6f;
            foreach (string line in KitPaint.Wrap(TipFont, _text, MaxWidth * Kit.Scale))
            {
                g.DrawText(TipFont, c.Surface, TipPad, y, line);
                y += lh;
            }
        }
    }

    /// <summary>
    /// A collapsible section for windows built from native Eto controls: a <see cref="KitPaint.SectionToggle"/>
    /// heading (chevron, UPPERCASE title, optional count badge) with section-level buttons on the right, and the
    /// content below it. The buttons stay visible while the section is closed. The open/closed state is kept in the
    /// window's <see cref="KitSections"/> under <paramref name="key"/>. The heading's hover area reaches 4 px past the
    /// chevron, so the section insets everything by 4 px: give its layout 4 px less side padding than usual (12, not 16)
    /// and its chevron, content and buttons line up with the window's other content.
    /// </summary>
    internal sealed class KitSection : StackLayout
    {
        private readonly KitSections _sections;
        private readonly string _key;
        private readonly Toggle _toggle;
        private readonly Panel _body;

        /// <summary>Inset in px on both sides, so the heading's hover area fits inside the section.</summary>
        public const int Inset = 4;

        /// <summary>Raised after the section opened or closed.</summary>
        public event EventHandler Toggled;

        public KitSection(KitSections sections, string key, string title, Control content, params Control[] buttons)
        {
            _sections = sections;
            _key = key;
            _toggle = new Toggle(this, title);

            var heading = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Padding = new Padding(0, 0, Inset, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            heading.Items.Add(new StackLayoutItem(_toggle, true));
            foreach (Control b in buttons) heading.Items.Add(b);

            // The gap under the heading belongs to the content, so a closed section ends right under its heading.
            Spacing = 0;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            Items.Add(heading);
            _body = new Panel { Content = content, Padding = new Padding(Inset, 8, Inset, 0) };
            Items.Add(_body);
            _body.Visible = IsOpen;
        }

        public bool IsOpen => _sections.IsOpen(_key);

        /// <summary>Count shown in a badge after the title (null for none), so a closed section still says what's in it.</summary>
        public string Count
        {
            get => _toggle.Count;
            set
            {
                if (_toggle.Count == value) return;
                _toggle.Count = value;
                _toggle.Invalidate();
            }
        }

        /// <summary>Opens or closes the section.</summary>
        public void Flip()
        {
            _sections.Toggle(_key);
            _body.Visible = IsOpen;
            _toggle.Invalidate();
            Toggled?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>The clickable heading: chevron, title and badge.</summary>
        private sealed class Toggle : KitCanvas
        {
            private readonly KitSection _owner;
            private readonly string _title;

            public Toggle(KitSection owner, string title)
            {
                _owner = owner;
                _title = title;
                Height = (int)Math.Ceiling(Kit.ControlH * S);
            }

            protected override bool RingOnFocus => true;

            public string Count { get; set; }

            protected override void PaintContent(Graphics g)
            {
                bool open = _owner.IsOpen;
                // The hit area reaches 4 px left of the chevron, so start there to keep it inside the control.
                RectangleF hit = KitPaint.SectionToggle(g, 4, 0, _title, open, StateOf("toggle"), Count);
                AddTarget("toggle", hit, _owner.Flip, null,
                    open ? $"Close {_title.ToLowerInvariant()}" : $"Open {_title.ToLowerInvariant()}");
            }
        }
    }
}
