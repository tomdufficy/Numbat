using System;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;
using Numbat.UI;

namespace Numbat.Commands.Analysis.NumbatGfa
{
    /// <summary>
    /// The small floating GFA window: Numbat's title bar over a scroller around <see cref="GfaView"/>, which draws
    /// everything with the Numbat UI kit. This class only handles opening, position, size and the saved view.
    /// </summary>
    internal sealed class GfaWindow : KitWindow
    {
        // One width for both views so switching never resizes the window. The user can resize it; the size is
        // remembered. Content taller than the window scrolls.
        private const int DefaultWidth = 330;
        private const int MinWidth = 280;
        private const int MinHeight = 240;

        // Width kept free on the right for the vertical scrollbar, whether or not it is showing, so the content
        // (header buttons, cards) doesn't shift sideways when switching between bars and pie.
        private const int ScrollbarGutter = 17;

        // Remembered between openings and Rhino sessions.
        private const string KeyX = "window.x";
        private const string KeyY = "window.y";
        private const string KeyView = "window.view";
        private const string KeyW = "window.w";
        private const string KeyH = "window.h";

        private static GfaWindow _instance;
        private readonly GfaModel _model;
        private readonly GfaView _view;
        private readonly Scrollable _scroll;

        private GfaWindow(GfaModel model) : base(GfaInfo.Title)
        {
            _model = model;
            bool pie = NumbatPlugin.Instance.Settings.GetString(KeyView, "bars") == "pie";

            Resizable = true;
            MinimumSize = new Size((int)(MinWidth * Kit.Scale), (int)(MinHeight * Kit.Scale));

            _view = new GfaView(model, pie);
            _scroll = new Scrollable
            {
                Content = _view,
                Border = BorderType.None,
                BackgroundColor = Kit.Colours.Surface,
                ExpandContentWidth = false,
                ExpandContentHeight = true,
            };
            _view.ViewportHeight = () => _scroll.ClientSize.Height;
            _view.SizeChanged += (s, e) => _view.UpdateHeight();
            _scroll.SizeChanged += (s, e) => FitViewWidth();
            _view.ViewChanged += (s, e) =>
            {
                NumbatPlugin.Instance.Settings.SetString(KeyView, _view.Pie ? "pie" : "bars");
                TitleBar.Invalidate();
            };
            _view.DisplayChanged += (s, e) => OnDisplayChanged();
            Body = _scroll;

            TitleBar.AddButton(new KitTitleButton
            {
                Id = "chart", Icon = () => _view.Pie ? KitIcon.Bars : KitIcon.Pie,
                Tip = () => _view.Pie ? "Show as bars" : "Show as pie chart",
                Click = p => _view.ToggleView(),
            });
            TitleBar.AddButton(new KitTitleButton
            {
                Id = "help", Icon = () => KitIcon.Help, Tip = () => "How to use nbGFA (F1)", Click = p => HelpWindow.ShowWindow(),
            });
            TitleBar.AddButton(new KitTitleButton
            {
                Id = "more", Icon = () => KitIcon.More, Tip = () => "More options", Click = p => _view.ShowMainMenu(TitleBar, p),
            });

            _model.Changed += OnModelChanged;
            Closing += (s, e) => SaveBounds();
            Closed += (s, e) =>
            {
                _model.Changed -= OnModelChanged;
                _instance = null;
            };

            RestoreSize();
            _view.UpdateHeight();
        }

        /// <summary>
        /// Opens the window, or brings it to the front if it is already open.
        /// Closing it only hides the window: running the command again reopens it where it was.
        /// </summary>
        public static void ShowWindow(GfaModel model)
        {
            model.Refresh();
            if (_instance != null)
            {
                _instance.BringToFront();
                return;
            }

            Display.Load();
            _instance = new GfaWindow(model) { Owner = RhinoEtoApp.MainWindow };
            _instance.RestoreLocation();
            _instance.Show();
            _instance.BringToFront();
        }

        private void RestoreLocation()
        {
            var settings = NumbatPlugin.Instance.Settings;
            int x = settings.GetInteger(KeyX, int.MinValue);
            int y = settings.GetInteger(KeyY, int.MinValue);
            if (x == int.MinValue || y == int.MinValue) return; // first time: let the OS place it

            // Only reuse the saved spot if the title bar would be on a connected screen,
            // so the window can't be "lost" after unplugging a monitor.
            var probe = new PointF(x + 40, y + 10);
            if (Screen.Screens.Any(s => s.WorkingArea.Contains(probe)))
            {
                Location = new Point(x, y);
            }
        }

        /// <summary>Saved size, or on first use a default width and a height that fits the content.</summary>
        private void RestoreSize()
        {
            var settings = NumbatPlugin.Instance.Settings;
            int w = settings.GetInteger(KeyW, -1);
            int h = settings.GetInteger(KeyH, -1);
            if (w < MinimumSize.Width || h < MinimumSize.Height)
            {
                w = (int)((DefaultWidth + ScrollbarGutter) * Kit.Scale);
                h = (int)Math.Ceiling(_view.ContentHeight(DefaultWidth) * Kit.Scale) + ChromeHeight;
            }
            ClientSize = new Size(w, Math.Max(h, MinimumSize.Height));
        }

        private void SaveBounds()
        {
            var settings = NumbatPlugin.Instance.Settings;
            settings.SetInteger(KeyX, Location.X);
            settings.SetInteger(KeyY, Location.Y);
            settings.SetInteger(KeyW, ClientSize.Width);
            settings.SetInteger(KeyH, ClientSize.Height);
        }

        private void FitViewWidth()
        {
            int width = Math.Max(1, _scroll.Width - ScrollbarGutter);
            if (_view.Width != width) _view.Width = width;
            _view.UpdateHeight();
        }

        private void OnModelChanged(object sender, EventArgs e) => _view.UpdateHeight();

        /// <summary>Interface size changed: scale the window with it so the layout stays the same.</summary>
        private void OnDisplayChanged()
        {
            float old = MinimumSize.Width / (float)MinWidth;
            float ratio = Kit.Scale / old;
            MinimumSize = new Size((int)(MinWidth * Kit.Scale), (int)(MinHeight * Kit.Scale));
            ClientSize = new Size((int)(ClientSize.Width * ratio), (int)(ClientSize.Height * ratio));
            TitleBar.UpdateSize();
            _view.UpdateHeight();
        }
    }
}
