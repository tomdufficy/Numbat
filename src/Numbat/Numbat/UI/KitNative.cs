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
