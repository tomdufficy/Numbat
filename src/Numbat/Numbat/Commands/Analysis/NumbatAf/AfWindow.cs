using System;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;
using Numbat.UI;

namespace Numbat.Commands.Analysis.NumbatAf
{
    /// <summary>
    /// The small floating nbAF window: Numbat's title bar over a scroller around <see cref="AfView"/>, which draws everything with the
    /// Numbat UI kit. This class handles opening, position and size, and switches the live preview on while the
    /// window is open.
    /// </summary>
    internal sealed class AfWindow : KitWindow
    {
        private const int DefaultWidth = 330;
        private const int MinWidth = 300;
        private const int MinHeight = 260;
        private const int ScrollbarGutter = 17;

        // Remembered between openings and Rhino sessions.
        private const string KeyX = AfInfo.SettingsPrefix + "window.x";
        private const string KeyY = AfInfo.SettingsPrefix + "window.y";
        private const string KeyW = AfInfo.SettingsPrefix + "window.w";
        private const string KeyH = AfInfo.SettingsPrefix + "window.h";

        private static AfWindow _instance;
        private readonly AfModel _model;
        private readonly AfView _view;
        private readonly Scrollable _scroll;

        private AfWindow(AfModel model) : base(AfInfo.Title)
        {
            _model = model;
            Resizable = true;
            MinimumSize = new Size((int)(MinWidth * Kit.Scale), (int)(MinHeight * Kit.Scale));

            _view = new AfView(model);
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
            _view.DisplayChanged += (s, e) => OnDisplayChanged();
            Body = _scroll;

            TitleBar.AddButton(new KitTitleButton
            {
                Id = "help", Icon = () => KitIcon.Help, Tip = () => "How to use nbAF (F1)", Click = p => AfHelp.Show(),
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
                _model.Live = false;
                AfPreviewConduit.Show(false, RhinoDoc.ActiveDoc);
                _instance = null;
            };

            RestoreSize();
            _view.UpdateHeight();
        }

        public static bool IsOpen => _instance != null;

        /// <summary>
        /// Opens the window (or brings it to the front), turns on the live preview and recalculates.
        /// Closing the window turns the preview off; baked output stays.
        /// </summary>
        public static void ShowWindow(AfModel model, RhinoDoc doc)
        {
            model.Live = true;
            model.Refresh(doc);
            AfPreviewConduit.Show(true, doc);
            if (_instance != null)
            {
                _instance.BringToFront();
                return;
            }

            Display.Load();
            _instance = new AfWindow(model) { Owner = RhinoEtoApp.MainWindow };
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

            // Only reuse the saved spot if the title bar would be on a connected screen.
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
                h = (int)Math.Ceiling(Math.Min(_view.ContentHeight(DefaultWidth), 760) * Kit.Scale) + ChromeHeight;
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

    /// <summary>Shows docs/nbAF.md in the kit's guide window, followed by the sources and their saved copies.</summary>
    internal static class AfHelp
    {
        public static void Show() =>
            KitHelpWindow.Show($"{AfInfo.Title} · Guide", "nbAF.md", AfInfo.Copyright, SourceControls);

        private static System.Collections.Generic.IEnumerable<Control> SourceControls()
        {
            KitPalette c = Kit.Colours;
            yield return KitHelpWindow.Heading("Sources");
            yield return KitHelpWindow.Text(
                "The legal texts were saved on the date shown and ship with Numbat, in case the online version moves or " +
                "changes. The online link gives the current version.", KitHelpWindow.BodyFont, c.Text2);
            foreach (AfSource s in AfSources.All)
            {
                AfSource source = s;
                yield return new Panel { Height = 2 };
                yield return KitHelpWindow.Text(s.Title, KitFonts.Scaled(12, Weight.Semibold), c.Text);
                yield return KitHelpWindow.Text(s.File != null ? $"{s.Note} Saved {s.Accessed}." : s.Note, KitHelpWindow.BodyFont, c.Text2);
                var links = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 8 };
                if (s.File != null)
                {
                    var saved = new KitButton("Open saved copy", ButtonKind.Ghost, KitIcon.Folder) { Enabled = s.HasLocalCopy };
                    saved.Click += (o, e) => AfSources.Open(source.LocalPath);
                    links.Items.Add(saved);
                }
                var online = new KitButton("Open online", ButtonKind.Ghost);
                online.Click += (o, e) => AfSources.Open(source.Url);
                links.Items.Add(online);
                yield return links;
            }
        }
    }
}
