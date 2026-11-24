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
        private const string KeyLength = AnonInfo.SettingsPrefix + "codeLength";
        private const string KeyPerPlace = AnonInfo.SettingsPrefix + "perPlace";

        /// <summary>Code lengths offered, in characters.</summary>
        private static readonly int[] Lengths = { 1, 2, 3, 4, 6, 8 };

        private static AnonWindow _instance;

        private readonly TextBox _phrase;
        private readonly KitSwitch _matchCase, _wholeWord;
        private readonly Dictionary<AnonArea, KitSwitch> _areas = new Dictionary<AnonArea, KitSwitch>();
        private readonly GridView _grid;
        private readonly KitSegmented _mode, _length;
        private readonly KitSwitch _perPlace;
        private readonly Control _lengthRow;
        private readonly TextBox _custom;
        private readonly Label _preview, _status;
        private readonly KitButton _apply, _report, _reroll;

        private readonly KitSections _sections = new KitSections(AnonInfo.SettingsPrefix + "closed");
        private readonly KitSection _find, _lookIn, _found, _replaceWith;
        private readonly KitHeaderBar _header;

        private List<AnonHit> _hits = new List<AnonHit>();
        private AnonMatcher _matcher;
        private AnonReplacements _plan;

        private AnonWindow() : base(AnonInfo.Title)
        {
            Resizable = true;
            var s = NumbatPlugin.Instance.Settings;
            int width = (int)(ContentWidth * Kit.Scale);

            // ---- find
            _phrase = new TextBox { PlaceholderText = "Names to find, separated by commas, e.g. ACME, Hamburg" };
            _phrase.KeyDown += (o, e) => { if (e.Key == Keys.Enter) { e.Handled = true; Scan(); } };
            _phrase.TextChanged += (o, e) => UpdateCounts();
            _matchCase = new KitSwitch("Match case", s.GetBool(KeyCase, false));
            _wholeWord = new KitSwitch("Whole words only", s.GetBool(KeyWhole, false));
            var scan = new KitButton("Scan", ButtonKind.Secondary, KitIcon.Pick) { Tip = "Look for every name (separate them with commas) in the places ticked below" };
            scan.Click += (o, e) => Scan();

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
                AutoSize = false,
                DataCell = new CheckBoxCell
                {
                    Binding = Binding.Delegate<AnonHit, bool?>(h => h.Editable && h.Include, (h, v) =>
                    {
                        if (h.Editable) h.Include = v ?? false;
                        UpdateStatus();
                    }),
                },
            });
            // Fixed columns: long names end in "…" and show in full on hover, so the window never scrolls sideways.
            var text = new KitGridText(_grid);
            text.Add<AnonHit>("Where", 220, h => h.Where);
            text.Add<AnonHit>("Now", 160, h => h.Text, expand: true);
            text.Add<AnonHit>("After", 160, h => h.Editable ? h.After : "report only", expand: true, secondary: h => !h.Editable);

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
                ShowModeControls();
                NewReplacement();
            };
            _length = new KitSegmented(Lengths.Select(n => n.ToString()), Math.Max(0, Array.IndexOf(Lengths, s.GetInteger(KeyLength, 6))));
            _length.SelectedIndexChanged += (o, e) =>
            {
                NumbatPlugin.Instance.Settings.SetInteger(KeyLength, Length);
                NewReplacement();
            };
            _lengthRow = KitForm.HStack(KitForm.Label("Length"), _length, KitForm.Caption("characters"));
            _perPlace = new KitSwitch("A different one in every place", s.GetBool(KeyPerPlace, false))
            {
                ToolTip = "Like code words: every place gets its own random code or word, so places can't be matched up afterwards. " +
                          "Off: each name becomes the same code or word everywhere, so the layer, the block and the note still match.",
            };
            _perPlace.CheckedChanged += (o, e) =>
            {
                NumbatPlugin.Instance.Settings.SetBool(KeyPerPlace, _perPlace.Checked);
                NewReplacement();
            };
            _custom = new TextBox { PlaceholderText = "Replacement text" };
            _custom.TextChanged += (o, e) => { if (Mode == ReplaceMode.Text) NewReplacement(); };
            _reroll = new KitButton("New", ButtonKind.Ghost, KitIcon.Reset) { Tip = "Pick other random codes or words" };
            _reroll.Click += (o, e) => NewReplacement();
            _preview = KitForm.Caption(string.Empty, width - (int)(40 * Kit.Scale)); // wraps, so a long phrase can't widen the window

            _status = KitForm.Body("Type a phrase, choose where to look and click Scan.", width - (int)(40 * Kit.Scale), secondary: true);
            _report = new KitButton("Save report…", ButtonKind.Ghost, KitIcon.Folder) { Tip = "Save the list as a text file", Enabled = false };
            _report.Click += (o, e) => SaveReport();
            _apply = new KitButton("Anonymise", ButtonKind.Primary) { Tip = "Replace the names in every ticked place (one undo step)", Enabled = false };
            _apply.Click += (o, e) => Apply();

            var footer = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalContentAlignment = VerticalAlignment.Center,
                Items = { _report, new StackLayoutItem(null, true), _apply },
            };

            // ---- sections: each opens and closes; Tick all / none stay usable while Found is closed
            _find = new KitSection(_sections, "find", "Find", KitForm.VStack(8, new StackLayout
            {
                Orientation = Orientation.Horizontal, Spacing = 8, VerticalContentAlignment = VerticalAlignment.Center,
                Items = { new StackLayoutItem(_phrase, true), scan },
            }, KitForm.HStack(_matchCase, _wholeWord)));
            _lookIn = new KitSection(_sections, "lookin", "Look in", areaTable);
            _found = new KitSection(_sections, "found", "Found", KitForm.VStack(8, _grid, _status), all, none);
            _replaceWith = new KitSection(_sections, "replace", "Replace with", KitForm.VStack(8, KitForm.HStack(_mode, _reroll), _lengthRow, _custom, _perPlace, _preview));
            foreach (KitSwitch sw in _areas.Values) sw.CheckedChanged += (o, e) => UpdateCounts();

            _header = new KitHeaderBar(AnonInfo.Name)
                .Add(KitIcon.More, "More options", (bar, p) => ShowMainMenu(bar, p))
                .Add(KitIcon.Help, "How to use nbAnon (F1)", AnonHelp.Show);
            _header.HelpRequested += (o, e) => AnonHelp.Show();

            var content = new StackLayout
            {
                Padding = new Padding(12, 12, 12, 8),
                Spacing = 12,
                BackgroundColor = Kit.Colours.Surface,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items = { _find, _lookIn, _found, _replaceWith },
            };

            Body = new StackLayout
            {
                BackgroundColor = Kit.Colours.Surface,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items =
                {
                    _header,
                    new StackLayoutItem(new Scrollable
                    {
                        Border = BorderType.None,
                        BackgroundColor = Kit.Colours.Surface,
                        Content = content,
                    }, true),
                    KitForm.Divider(),
                    new Panel { Padding = new Padding(16, 10, 16, 6), BackgroundColor = Kit.Colours.Surface, Content = footer },
                    new Panel { Padding = new Padding(16, 0, 16, 10), BackgroundColor = Kit.Colours.Surface, Content = KitForm.Caption(AnonInfo.Copyright) },
                },
            };

            Closing += (o, e) => SaveBounds();
            Closed += (o, e) => _instance = null;
            RestorePlace(width);
            ShowModeControls();
            NewReplacement();
            UpdateCounts();
        }

        /// <summary>Length only for codes, the text box only for My text; New and per place only for the random modes.</summary>
        private void ShowModeControls()
        {
            bool random = Mode == ReplaceMode.RandomLetters || Mode == ReplaceMode.RandomWord;
            _lengthRow.Visible = Mode == ReplaceMode.RandomLetters;
            _custom.Visible = Mode == ReplaceMode.Text;
            _perPlace.Visible = random;
            _reroll.Enabled = random;
        }

        private int Length => Lengths[Math.Max(0, Math.Min(Lengths.Length - 1, _length.SelectedIndex))];

        /// <summary>The names in the box now (the scanned ones may differ until Scan is clicked again).</summary>
        private List<string> TypedPhrases => AnonMatcher.Split(_phrase.Text);

        /// <summary>Badges on the section headings, so a closed section still says what's in it.</summary>
        private void UpdateCounts()
        {
            int phrases = TypedPhrases.Count;
            _find.Count = phrases > 1 ? phrases.ToString() : null;
            _lookIn.Count = $"{_areas.Values.Count(a => a.Checked)}/{_areas.Count}";
            _found.Count = _matcher == null ? null : _hits.Count.ToString();
        }

        /// <summary>The window's ⋯ menu.</summary>
        private void ShowMainMenu(Control owner, PointF location)
        {
            var menu = new ContextMenu();
            var help = new ButtonMenuItem { Text = "How to use nbAnon" };
            help.Click += (o, e) => AnonHelp.Show();
            menu.Items.Add(help);
            menu.Items.Add(new SeparatorMenuItem());
            var report = new ButtonMenuItem { Text = "Save report…", Enabled = _hits.Count > 0 };
            report.Click += (o, e) => SaveReport();
            menu.Items.Add(report);
            menu.Show(owner, location);
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
            List<string> phrases = TypedPhrases;
            if (doc == null || phrases.Count == 0)
            {
                _status.Text = "Type the names to look for first.";
                return;
            }
            var areas = _areas.Where(a => a.Value.Checked).Select(a => a.Key).ToList();
            var s = NumbatPlugin.Instance.Settings;
            s.SetBool(KeyCase, _matchCase.Checked);
            s.SetBool(KeyWhole, _wholeWord.Checked);
            s.SetString(KeyAreas, string.Join(",", areas));

            _matcher = new AnonMatcher(phrases, _matchCase.Checked, _wholeWord.Checked);
            _hits = AnonScanner.Scan(doc, _matcher, areas);
            // Keep the replacements unless the number of names changed.
            if (_plan == null || _plan.Shared.Count != _matcher.Phrases.Count) NewReplacement();
            else Preview();
        }

        /// <summary>Draws new replacements for the current mode and names, then refreshes the preview.</summary>
        private void NewReplacement()
        {
            int count = _matcher?.Phrases.Count ?? Math.Max(1, TypedPhrases.Count);
            _plan = new AnonReplacements(Mode, Length, _perPlace.Checked, _custom.Text, count);
            Preview();
        }

        /// <summary>Works out every "After" and refreshes the list and the buttons.</summary>
        private void Preview()
        {
            _preview.Text = PlanText();
            foreach (AnonHit h in _hits)
            {
                // Each place keeps the replacements it is previewed with, so Anonymise writes exactly what the list shows.
                h.With = h.Editable ? _plan.ForPlace() : null;
                h.Fallback = _plan.Fallback();
                h.After = h.Editable ? Rewrite(h, h.Text, h.IsName) : null;
            }
            _grid.DataStore = _hits.ToList();
            UpdateStatus();
        }

        /// <summary>What the names become, in words, under Replace with.</summary>
        private string PlanText()
        {
            IReadOnlyList<string> names = _matcher?.Phrases ?? TypedPhrases;
            if (names.Count == 0 || _plan == null) return string.Empty;
            string label = string.Join(", ", names.Select(n => $"\"{n}\""));
            bool one = names.Count == 1;
            switch (Mode)
            {
                case ReplaceMode.Remove:
                    return $"{label} {(one ? "is" : "are")} taken out. Names that would be empty get a random code.";
                case ReplaceMode.Text:
                    return $"{label} {(one ? "becomes" : "all become")} \"{_plan.Text}\" everywhere.";
            }
            string kind = Mode == ReplaceMode.RandomWord ? "word" : "code";
            if (_plan.PerPlace)
                return $"Every place gets its own random {kind} for {label}: see After in the list.";
            return string.Join(", ", names.Select((n, i) => $"\"{n}\" becomes \"{(i < _plan.Shared.Count ? _plan.Shared[i] : "")}\"")) + " everywhere.";
        }

        private string Rewrite(AnonHit hit, string text, bool isName)
        {
            string result = _matcher.Replace(text, hit.With ?? _plan.Shared);
            return isName && string.IsNullOrWhiteSpace(result) ? hit.Fallback ?? _plan.Fallback() : result;
        }

        private void UpdateStatus()
        {
            int editable = _hits.Count(h => h.Editable);
            int ticked = _hits.Count(h => h.Editable && h.Include);
            int reportOnly = _hits.Count - editable;
            if (_matcher == null) return;
            _status.Text = _hits.Count == 0
                ? $"{_matcher.Label} {(_matcher.Phrases.Count == 1 ? "was" : "were")} not found in the places ticked above."
                : $"Found in {_hits.Count} place(s): {ticked} of {editable} ticked to change" +
                  (reportOnly > 0 ? $", {reportOnly} report only (file paths and snapshots: rename those yourself)." : ".");
            _apply.Enabled = ticked > 0;
            _apply.Text = ticked > 0 ? $"Anonymise {ticked}" : "Anonymise";
            _report.Enabled = _hits.Count > 0;
            UpdateCounts();
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
            if (MessageBox.Show(this, $"{ConfirmText(chosen.Count)}\n\n" +
                    "Undo (Ctrl+Z) reverses it, except for group names, named CPlanes and the document notes: Rhino can't " +
                    "undo those. Save the report first if you may need the old names.",
                    AnonInfo.Title, MessageBoxButtons.OKCancel, MessageBoxType.Question) != DialogResult.Ok)
            {
                return;
            }

            int failed = 0;
            uint undo = doc.BeginUndoRecord($"{AnonInfo.Name}: replace {_matcher.Label}");
            try
            {
                foreach (AnonHit h in chosen)
                {
                    try
                    {
                        AnonHit hit = h;
                        h.Apply(doc, (text, isName) => Rewrite(hit, text, isName));
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
            string label = _matcher.Label;
            Scan();
            RhinoApp.WriteLine($"{AnonInfo.Name}: changed {chosen.Count - failed} place(s); {label} still found in {_hits.Count}." +
                               (failed > 0 ? $" {failed} could not be changed (see above)." : ""));
        }

        /// <summary>The question before anonymising.</summary>
        private string ConfirmText(int places)
        {
            string label = _matcher.Label;
            string kind = Mode == ReplaceMode.RandomWord ? "word" : "code";
            switch (Mode)
            {
                case ReplaceMode.Remove: return $"Take {label} out in {places} place(s)?";
                case ReplaceMode.Text: return $"Replace {label} with \"{_plan.Text}\" in {places} place(s)?";
            }
            if (_plan.PerPlace) return $"Replace {label} with a different random {kind} in each of {places} place(s)?";
            string pairs = string.Join(", ", _matcher.Phrases.Select((n, i) => $"\"{n}\" with \"{_plan.Shared[i]}\""));
            return $"Replace {pairs} in {places} place(s)?";
        }

        private void SaveReport()
        {
            var dialog = new SaveFileDialog { Title = $"{AnonInfo.Name} – Save report", FileName = "nbAnon report.txt" };
            dialog.Filters.Add(new FileFilter("Text", ".txt"));
            if (dialog.ShowDialog(this) != DialogResult.Ok) return;
            string path = dialog.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? dialog.FileName : dialog.FileName + ".txt";

            var sb = new StringBuilder();
            sb.AppendLine($"{AnonInfo.Title} report, {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Looked for: {_matcher?.Label}{(_matchCase.Checked ? ", match case" : "")}{(_wholeWord.Checked ? ", whole words" : "")}");
            sb.AppendLine($"Replacement: {PlanText()}");
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
