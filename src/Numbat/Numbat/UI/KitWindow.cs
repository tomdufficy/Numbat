using System;
using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;

namespace Numbat.UI
{
    /// <summary>A button in a <see cref="KitTitleBar"/>: an icon, its tip and what it does.</summary>
    internal sealed class KitTitleButton
    {
        public string Id;
        public Func<KitIcon> Icon;
        public Func<string> Tip;
        /// <summary>Runs on click with the point just below the button, in title-bar control pixels (for menus).</summary>
        public Action<PointF> Click;
    }

    /// <summary>
    /// Numbat's title bar: a brown band with the window's name in white ("nbGFA – Gross Floor Area"), icon buttons
    /// on the right and a close button at the end. It replaces the system title bar (see <see cref="KitWindow"/>),
    /// so dragging an empty part of it moves the window. Brown with white text in both light and dark mode.
    /// </summary>
    internal sealed class KitTitleBar : KitCanvas
    {
        private readonly Window _window;
        private readonly List<KitTitleButton> _buttons = new List<KitTitleButton>();
        private string _title;
        private bool _dragging;
        private PointF _dragMouse;
        private Point _dragStart;

        protected override bool DrawnTips => false;

        protected override Color CanvasBackground => Kit.Colours.TitleBar;

        public KitTitleBar(Window window, string title, bool closeButton = true)
        {
            _window = window;
            _title = title;
            Height = (int)Math.Ceiling(Kit.TitleH * S);
            if (closeButton)
            {
                _closeButton = new KitTitleButton
                {
                    Id = "close", Icon = () => KitIcon.Close, Tip = () => "Close", Click = p => _window.Close(),
                };
            }
            MouseMove += OnDragMove;
            MouseUp += (s, e) => EndDrag();
            LostFocus += (s, e) => EndDrag();
        }

        private readonly KitTitleButton _closeButton;

        public string Title
        {
            get => _title;
            set { _title = value; Invalidate(); }
        }

        /// <summary>Adds an icon button left of the close button (added left to right).</summary>
        public void AddButton(KitTitleButton button)
        {
            _buttons.Add(button);
            Invalidate();
        }

        /// <summary>Lets a dialog route the close button to its Cancel.</summary>
        public Action CloseAction
        {
            set { if (_closeButton != null) _closeButton.Click = p => value(); }
        }

        /// <summary>Call after the interface size changes.</summary>
        public void UpdateSize()
        {
            Height = (int)Math.Ceiling(Kit.TitleH * S);
            Invalidate();
        }

        protected override void PaintContent(Graphics g)
        {
            KitPalette c = Kit.Colours;
            float w = LogicalWidth, h = Kit.TitleH;
            var all = new List<KitTitleButton>(_buttons);
            if (_closeButton != null) all.Add(_closeButton);

            float right = w - 4;
            float buttonsLeft = right - all.Count * Kit.ControlH;
            for (int i = 0; i < all.Count; i++)
            {
                KitTitleButton b = all[i];
                var r = new RectangleF(buttonsLeft + i * Kit.ControlH, (h - Kit.ControlH) / 2, Kit.ControlH, Kit.ControlH);
                PartState st = StateOf(b.Id);
                if (st.Hover || st.Pressed)
                {
                    Color hover = b == _closeButton ? Kit.Hex(0xC42B1C) : Color.FromArgb(255, 255, 255, st.Pressed ? 56 : 32);
                    KitPaint.Fill(g, r, Kit.RadiusControl, hover);
                }
                KitIcons.Draw(g, b.Icon(), RectangleF.Inflate(r, -(Kit.ControlH - Kit.IconSize) / 2, -(Kit.ControlH - Kit.IconSize) / 2), c.OnTitleBar);
                if (st.Focused) KitPaint.Stroke(g, RectangleF.Inflate(r, -1, -1), Kit.RadiusControl, c.OnTitleBar, 2);
                KitTitleButton button = b;
                AddTarget(b.Id, r, () => button.Click(ToControl(new PointF(r.X, r.Bottom + 2))), null, b.Tip());
            }

            string title = KitPaint.Fit(KitFonts.Title, _title, buttonsLeft - Kit.Pad - 8);
            KitPaint.TextMiddle(g, KitFonts.Title, c.OnTitleBar, Kit.Pad, 0, h, title);
        }

        // ---- moving the window --------------------------------------------------------------

        protected override void OnEmptyMouseDown(MouseEventArgs e)
        {
            if (e.Buttons != MouseButtons.Primary) return;
            _dragging = true;
            _dragMouse = Mouse.Position;
            _dragStart = _window.Location;
            CaptureMouse();
        }

        private void OnDragMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            if (!e.Buttons.HasFlag(MouseButtons.Primary))
            {
                EndDrag();
                return;
            }
            PointF now = Mouse.Position;
            _window.Location = new Point(_dragStart.X + (int)Math.Round(now.X - _dragMouse.X), _dragStart.Y + (int)Math.Round(now.Y - _dragMouse.Y));
        }

        private void EndDrag()
        {
            if (!_dragging) return;
            _dragging = false;
            ReleaseMouseCapture();
        }
    }

    /// <summary>
    /// Floating tool window in Numbat's style: no system title bar, a brown <see cref="KitTitleBar"/> with the full
    /// name ("nbAF – Abstandsflächen") instead, a thin border, and the body below. Stays on top of Rhino and keeps
    /// Windows' resize edges when <see cref="Window.Resizable"/> is set.
    /// </summary>
    internal class KitWindow : FloatingForm
    {
        private readonly Panel _body = new Panel();

        public KitWindow(string title)
        {
            Title = title; // still shown in Alt+Tab and the taskbar
            WindowStyle = WindowStyle.None;
            Minimizable = false;
            Maximizable = false;
            ShowInTaskbar = false;
            Topmost = true;
            KitPalette c = Kit.Colours;
            BackgroundColor = c.Surface;
            TitleBar = new KitTitleBar(this, title);
            _body.BackgroundColor = c.Surface;
            Content = Frame(TitleBar, _body);
        }

        public KitTitleBar TitleBar { get; }

        /// <summary>The window's content below the title bar.</summary>
        public Control Body
        {
            get => _body.Content;
            set => _body.Content = value;
        }

        /// <summary>Height of the title bar and border in control pixels, for sizing the window to its body.</summary>
        public static int ChromeHeight => (int)Math.Ceiling(Kit.TitleH * Kit.Scale) + 2;

        /// <summary>
        /// Title bar on top of the body, inside a 1 px border: without the system frame a light window would otherwise
        /// blend into a light background.
        /// </summary>
        internal static Control Frame(KitTitleBar bar, Control body)
        {
            KitPalette c = Kit.Colours;
            return new Panel
            {
                Padding = new Padding(1),
                BackgroundColor = c.Outline,
                Content = new StackLayout
                {
                    Spacing = 0,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    BackgroundColor = c.Surface,
                    Items = { bar, new StackLayoutItem(body, true) },
                },
            };
        }
    }
}
