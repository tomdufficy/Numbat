using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;
using Numbat.UI;
using SaveFileDialog = Eto.Forms.SaveFileDialog;

namespace Numbat.Commands.Utilities.NumbatAnon
{
    /// <summary>
    /// The nbAnon window: type a phrase (a company or client name), choose where to look, Scan, review every place it
    /// was found with a tick box each, choose what replaces it, then Anonymise (one undo step). The report can be
    /// saved as a text file.
    /// </summary>
    internal sealed class AnonWindow : KitWindow
    {
        private const int ContentWidth = 600;
        private const string KeyX = AnonInfo.SettingsPrefix + "window.x";
        private const string KeyY = AnonInfo.SettingsPrefix + "window.y";
        private const string KeyW = AnonInfo.SettingsPrefix + "window.w";
        private const string KeyH = AnonInfo.SettingsPrefix + "window.h";
        private const string KeyCase = AnonInfo.SettingsPrefix + "matchCase";
        private const string KeyWhole = AnonInfo.SettingsPrefix + "wholeWord";
        private const string KeyMode = AnonInfo.SettingsPrefix + "mode";
        private const string KeyAreas = AnonInfo.SettingsPrefix + "areas";

        private static AnonWindow _instance;

        private readonly TextBox _phrase;
        private readonly KitSwitch _matchCase, _wholeWord;
        private readonly Dictionary<AnonArea, KitSwitch> _areas = new Dictionary<AnonArea, KitSwitch>();
        private readonly GridView _grid;
        private readonly KitSegmented _mode;
        private readonly TextBox _custom;
        private readonly Label _preview, _status;
        private readonly KitButton _apply, _report;

        private List<AnonHit> _hits = new List<AnonHit>();
        private AnonMatcher _matcher;
        private string _replacement = string.Empty;

        private AnonWindow() : base(AnonInfo.Title)
        {
            Resizable = true;
            var s = NumbatPlugin.Instance.Settings;
            int width = (int)(ContentWidth * Kit.Scale);

            // ---- find
            _phrase = new TextBox { PlaceholderText = "Company or client name, e.g. ACME" };
            _phrase.KeyDown += (o, e) => { if (e.Key == Keys.Enter) { e.Handled = true; Scan(); } };
            _matchCase = new KitSwitch("Match case", s.GetBool(KeyCase, false));
            _wholeWord = new KitSwitch("Whole words only", s.GetBool(KeyWhole, false));
            var scan = new KitButton("Scan", ButtonKind.Secondary, KitIcon.Pick) { Tip = "Look for the phrase in the places ticked below" };
            scan.Click += (o, e) => Scan();
            var help = new KitButton(null, ButtonKind.Ghost, KitIcon.Help) { Tip = "How to use nbAnon" };
            help.Click += (o, e) => AnonHelp.Show();

            // ---- where
            string saved = s.GetString(KeyAreas, string.Join(",", AnonScanner.Areas.Select(a => a.area)));
            var areaTable = new TableLayout { Spacing = new Size(16, 4) };
            TableRow row = null;
            for (int i = 0; i < AnonScanner.Areas.Count; i++)
            {
                var a = AnonScanner.Areas[i];
                var sw = new KitSwitch(a.label, saved.Split(',').Contains(a.area.ToString())) { ToolTip = a.tip };
                _areas[a.area] = sw;
                if (i % 2 == 0)
                {
                    row = new TableRow();
                    areaTable.Rows.Add(row);
                }
                row.Cells.Add(new TableCell(sw, true));
            }
            if (AnonScanner.Areas.Count % 2 == 1) row.Cells.Add(new TableCell(null, true));

            // ---- results
            _grid = new GridView
            {
                AllowMultipleSelection = false,
                ShowHeader = true,
                Height = (int)(240 * Kit.Scale),
            };
            _grid.Columns.Add(new GridColumn
            {
                HeaderText = "",
                Editable = true,
                Width = (int)(28 * Kit.Scale),
                DataCell = new CheckBoxCell
                {
                    Binding = Binding.Delegate<AnonHit, bool?>(h => h.Editable && h.Include, (h, v) =>
                    {
                        if (h.Editable) h.Include = v ?? false;
                        UpdateStatus();
                    }),
                },
            });
            _grid.Columns.Add(new GridColumn { HeaderText = "Where", Width = (int)(240 * Kit.Scale), DataCell = new TextBoxCell { Binding = Binding.Delegate<AnonHit, string>(h => h.Where) } });
            _grid.Columns.Add(new GridColumn { HeaderText = "Now", Width = (int)(160 * Kit.Scale), DataCell = new TextBoxCell { Binding = Binding.Delegate<AnonHit, string>(h => OneLine(h.Text)) } });
            _grid.Columns.Add(new GridColumn { HeaderText = "After", Width = (int)(160 * Kit.Scale), DataCell = new TextBoxCell { Binding = Binding.Delegate<AnonHit, string>(h => h.Editable ? OneLine(h.After) : "report only") } });

            var all = new KitButton("Tick all", ButtonKind.Ghost);
            all.Click += (o, e) => SetAll(true);
            var none = new KitButton("Tick none", ButtonKind.Ghost);
            none.Click += (o, e) => SetAll(false);

            // ---- replace with
            string[] modes = { "Random code", "Random word", "My text", "Remove" };
            _mode = new KitSegmented(modes, s.GetInteger(KeyMode, 1));
            // Equal segments, each wide enough for the longest label.
            float segment = modes.Max(m => KitPaint.Measure(KitFonts.Label, m).Width) + 24;
            _mode.Width = (int)Math.Ceiling((segment * modes.Length + 4) * Kit.Scale);
            _mode.SelectedIndexChanged += (o, e) =>
            {
                NumbatPlugin.Instance.Settings.SetInteger(KeyMode, _mode.SelectedIndex);
                _custom.Enabled = Mode == ReplaceMode.Text;
                NewReplacement();
            };
            _custom = new TextBox { PlaceholderText = "Replacement text", Enabled = Mode == ReplaceMode.Text };
            _custom.TextChanged += (o, e) => { if (Mode == ReplaceMode.Text) NewReplacement(); };
            var reroll = new KitButton("New", ButtonKind.Ghost, KitIcon.Reset) { Tip = "Pick another random code or word" };
            reroll.Click += (o, e) => NewReplacement();
            _preview = KitForm.Caption(string.Empty);

            _status = KitForm.Body("Type a phrase, choose where to look and click Scan.", width, secondary: true);
            _report = new KitButton("Save report…", ButtonKind.Ghost, KitIcon.Folder) { Tip = "Save the list as a text file", Enabled = false };
            _report.Click += (o, e) => SaveReport();
            _apply = new KitButton("Anonymise", ButtonKind.Primary) { Tip = "Replace the phrase in every ticked place (one undo step)", Enabled = false };
            _apply.Click += (o, e) => Apply();

            var footer = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalContentAlignment = VerticalAlignment.Center,
                Items = { _report, new StackLayoutItem(null, true), _apply },
            };

            Body = new Scrollable
            {
                Border = BorderType.None,
                BackgroundColor = Kit.Colours.Surface,
                Content = new StackLayout
                {
                    Padding = new Padding(16, 16, 16, 16),
                    Spacing = 10,
                    BackgroundColor = Kit.Colours.Surface,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Items =
                    {
                        new StackLayout
                        {
                            Orientation = Orientation.Horizontal, Spacing = 8, VerticalContentAlignment = VerticalAlignment.Center,
                            Items = { KitForm.Section("Find"), new StackLayoutItem(null, true), help },
                        },
                        KitForm.HStack(_phrase, scan),
                        KitForm.HStack(_matchCase, _wholeWord),
                        KitForm.Divider(),
                        KitForm.Section("Look in"),
                        areaTable,
                        KitForm.Divider(),
                        new StackLayout
                        {
                            Orientation = Orientation.Horizontal, Spacing = 8, VerticalContentAlignment = VerticalAlignment.Center,
                            Items = { KitForm.Section("Found"), new StackLayoutItem(null, true), all, none },
                        },
                        _grid,
                        _status,
                        KitForm.Divider(),
                        KitForm.Section("Replace with"),
                        KitForm.HStack(_mode, reroll),
                        _custom,
                        _preview,
                        KitForm.Divider(),
                        footer,
                    },
                },
            };
            _phrase.Width = width - (int)(110 * Kit.Scale);

            Closing += (o, e) => SaveBounds();
            Closed += (o, e) => _instance = null;
            RestorePlace(width);
            NewReplacement();
        }

        private ReplaceMode Mode => (ReplaceMode)Math.Max(0, _mode.SelectedIndex);

        /// <summary>Opens the window, or brings it to the front.</summary>
        public static void ShowWindow()
        {
            if (_instance != null)
            {
                _instance.BringToFront();
                return;
            }
            Display.Load();
            _instance = new AnonWindow { Owner = RhinoEtoApp.MainWindow };
            _instance.Show();
            _instance.BringToFront();
            _instance._phrase.Focus();
        }

        // ---- actions --------------------------------------------------------------------------

        private void Scan()
        {
            RhinoDoc doc = RhinoDoc.ActiveDoc;
            string phrase = (_phrase.Text ?? string.Empty).Trim();
            if (doc == null || phrase.Length == 0)
            {
                _status.Text = "Type the phrase to look for first.";
                return;
            }
            var areas = _areas.Where(a => a.Value.Checked).Select(a => a.Key).ToList();
            var s = NumbatPlugin.Instance.Settings;
            s.SetBool(KeyCase, _matchCase.Checked);
            s.SetBool(KeyWhole, _wholeWord.Checked);
            s.SetString(KeyAreas, string.Join(",", areas));

            _matcher = new AnonMatcher(phrase, _matchCase.Checked, _wholeWord.Checked);
            _hits = AnonScanner.Scan(doc, _matcher, areas);
            Preview();
        }

        private void NewReplacement()
        {
            _replacement = AnonWords.For(Mode, _custom.Text);
            Preview();
        }

        /// <summary>Works out every "After" and refreshes the list and the buttons.</summary>
        private void Preview()
        {
            string phrase = _matcher?.Phrase ?? (_phrase.Text ?? "").Trim();
            _preview.Text = phrase.Length == 0 ? string.Empty
                : Mode == ReplaceMode.Remove ? $"\"{phrase}\" is taken out. Names that would be empty get a random code."
                : $"\"{phrase}\" becomes \"{_replacement}\" everywhere.";
            foreach (AnonHit h in _hits) h.After = h.Editable ? Rewrite(h.Text, h.IsName) : null;
            _grid.DataStore = _hits.ToList();
            UpdateStatus();
        }

        private string Rewrite(string text, bool isName)
        {
            string result = _matcher.Replace(text, _replacement);
            return isName && string.IsNullOrWhiteSpace(result) ? AnonWords.Letters() : result;
        }

        private void UpdateStatus()
        {
            int editable = _hits.Count(h => h.Editable);
            int ticked = _hits.Count(h => h.Editable && h.Include);
            int reportOnly = _hits.Count - editable;
            if (_matcher == null) return;
            _status.Text = _hits.Count == 0
                ? $"\"{_matcher.Phrase}\" was not found in the places ticked above."
                : $"Found in {_hits.Count} place(s): {ticked} of {editable} ticked to change" +
                  (reportOnly > 0 ? $", {reportOnly} report only (file paths and snapshots: rename those yourself)." : ".");
            _apply.Enabled = ticked > 0;
            _apply.Text = ticked > 0 ? $"Anonymise {ticked}" : "Anonymise";
            _report.Enabled = _hits.Count > 0;
        }

        private void SetAll(bool on)
        {
            foreach (AnonHit h in _hits.Where(x => x.Editable)) h.Include = on;
            _grid.DataStore = _hits.ToList();
            UpdateStatus();
        }

        private void Apply()
        {
            RhinoDoc doc = RhinoDoc.ActiveDoc;
            List<AnonHit> chosen = _hits.Where(h => h.Editable && h.Include).ToList();
            if (doc == null || chosen.Count == 0) return;
            string with = Mode == ReplaceMode.Remove ? "nothing" : $"\"{_replacement}\"";
            if (MessageBox.Show(this, $"Replace \"{_matcher.Phrase}\" with {with} in {chosen.Count} place(s)?\n\n" +
                    "Undo (Ctrl+Z) reverses it, except for group names, named CPlanes and the document notes: Rhino can't " +
                    "undo those. Save the report first if you may need the old names.",
                    AnonInfo.Title, MessageBoxButtons.OKCancel, MessageBoxType.Question) != DialogResult.Ok)
            {
                return;
            }

            int failed = 0;
            uint undo = doc.BeginUndoRecord($"{AnonInfo.Name}: replace \"{_matcher.Phrase}\"");
            try
            {
                foreach (AnonHit h in chosen)
                {
                    try
                    {
                        h.Apply(doc, Rewrite);
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        RhinoApp.WriteLine($"{AnonInfo.Name}: could not change {h.Where}: {ex.Message}");
                    }
                }
            }
            finally
            {
                if (undo != 0) doc.EndUndoRecord(undo);
            }
            doc.Views.Redraw();

            // Look again, so the list shows what is left.
            string phrase = _matcher.Phrase;
            Scan();
            RhinoApp.WriteLine($"{AnonInfo.Name}: changed {chosen.Count - failed} place(s); \"{phrase}\" is still found in {_hits.Count}." +
                               (failed > 0 ? $" {failed} could not be changed (see above)." : ""));
        }

        private void SaveReport()
        {
            var dialog = new SaveFileDialog { Title = $"{AnonInfo.Name} – Save report", FileName = "nbAnon report.txt" };
            dialog.Filters.Add(new FileFilter("Text", ".txt"));
            if (dialog.ShowDialog(this) != DialogResult.Ok) return;
            string path = dialog.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? dialog.FileName : dialog.FileName + ".txt";

            var sb = new StringBuilder();
            sb.AppendLine($"{AnonInfo.Title} report, {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Phrase: \"{_matcher?.Phrase}\"{(_matchCase.Checked ? ", match case" : "")}{(_wholeWord.Checked ? ", whole words" : "")}");
            sb.AppendLine($"Looked in: {string.Join(", ", _areas.Where(a => a.Value.Checked).Select(a => a.Key))}");
            sb.AppendLine($"Found: {_hits.Count}");
            sb.AppendLine();
            foreach (AnonHit h in _hits)
            {
                string action = !h.Editable ? "REPORT ONLY" : h.Include ? "CHANGE" : "KEEP";
                sb.AppendLine($"[{action}] {h.Where}");
                sb.AppendLine($"    now:   {OneLine(h.Text)}");
                if (h.Editable && h.Include) sb.AppendLine($"    after: {OneLine(h.After)}");
            }
            try
            {
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                RhinoApp.WriteLine($"{AnonInfo.Name}: report saved to {path}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not save the report: {ex.Message}", AnonInfo.Title, MessageBoxType.Error);
            }
        }

        private static string OneLine(string s) => (s ?? string.Empty).Replace("\r", " ").Replace("\n", " ");

        // ---- position and size ------------------------------------------------------------------

        private void RestorePlace(int width)
        {
            var s = NumbatPlugin.Instance.Settings;
            int w = s.GetInteger(KeyW, -1), h = s.GetInteger(KeyH, -1);
            ClientSize = new Size(Math.Max(w, width + (int)(32 * Kit.Scale)), h > 200 ? h : (int)(720 * Kit.Scale));
            int x = s.GetInteger(KeyX, int.MinValue), y = s.GetInteger(KeyY, int.MinValue);
            if (x != int.MinValue && y != int.MinValue && Screen.Screens.Any(sc => sc.WorkingArea.Contains(new PointF(x + 40, y + 10))))
            {
                Location = new Point(x, y);
            }
        }

        private void SaveBounds()
        {
            var s = NumbatPlugin.Instance.Settings;
            s.SetInteger(KeyX, Location.X);
            s.SetInteger(KeyY, Location.Y);
            s.SetInteger(KeyW, ClientSize.Width);
            s.SetInteger(KeyH, ClientSize.Height);
        }
    }

    /// <summary>Shows docs/nbAnon.md in the kit's guide window.</summary>
    internal static class AnonHelp
    {
        public static void Show() => KitHelpWindow.Show($"{AnonInfo.Title} · Guide", "nbAnon.md", AnonInfo.Copyright);
    }
}
