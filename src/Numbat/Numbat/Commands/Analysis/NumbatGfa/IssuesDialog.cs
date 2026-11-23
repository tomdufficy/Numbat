using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Forms;
using Rhino;
using Numbat.UI;

namespace Numbat.Commands.Analysis.NumbatGfa
{
    /// <summary>Lists problem volumes with buttons to select, flatten or ignore them.</summary>
    internal sealed class IssuesDialog : KitDialog<bool>
    {
        private const int TextWidth = 420;

        private readonly GfaModel _model;
        private readonly RhinoDoc _doc;

        private IssuesDialog(GfaModel model, RhinoDoc doc) : base($"{GfaInfo.Name} – Check volumes")
        {
            _model = model;
            _doc = doc;
            Build();
        }

        public static void Show(Window owner, GfaModel model, RhinoDoc doc)
        {
            if (doc == null) return;
            Display.Load();
            new IssuesDialog(model, doc).ShowModal(owner);
        }

        private void Build()
        {
            CheckReport report = _model.Check(_doc);
            // Every wrapping label gets an explicit width: without one, WPF measures it on a single line and
            // sizes the dialog too small, clipping the text and buttons.
            var layout = new StackLayout { Spacing = 16, Width = TextWidth, HorizontalContentAlignment = HorizontalAlignment.Stretch };

            if (report.IsClean)
            {
                layout.Items.Add(KitForm.Body(
                    "No issues found. All assigned volumes are closed, have a level bottom face and don't overlap.", TextWidth));
            }

            AddGroup(layout, report.Tilted, "Bottom slightly tilted", "counted",
                $"The bottom face is up to {Volumes.MaxTiltDegrees:0}° off level. GFA is counted from the tilted face. " +
                "Flatten rotates each volume level about the centre of its bottom face. Ignore keeps it as it is and stops flagging it.",
                withFixes: true);
            AddGroup(layout, report.NotClosed, "Not closed", "not counted",
                "Open polysurfaces have no volume. Cap or join them (Cap, Join), then they count again.");
            AddGroup(layout, report.NoFlatBottom, "No flat bottom", "not counted",
                $"No planar bottom face within {Volumes.MaxTiltDegrees:0}° of level was found. Model each storey as a box with a flat floor.");
            AddGroup(layout, report.NotSupported, "Not a polysurface", "not counted",
                "Meshes, surfaces and curves can't be measured. Convert meshes with MeshToNURB, or rebuild as boxes.");

            if (report.Overlaps.Count > 0)
            {
                List<Guid> ids = report.Overlaps.SelectMany(p => new[] { p.a, p.b }).Distinct().ToList();
                AddGroup(layout, ids, $"Overlapping volumes, {report.Overlaps.Count} pair(s)", "counted twice",
                    "These volumes share floor area at the same height, so that area is counted more than once. " +
                    "Trim or move them so they only touch.");
            }

            var again = new KitButton("Check again", ButtonKind.Ghost, KitIcon.Reset);
            again.Click += (s, e) => Build();
            var close = new KitButton("Close");
            close.Click += (s, e) => Close(false);
            AcceptButton = close;
            CancelButton = close;

            // The dialog auto-sizes to its content, including after a rebuild.
            SetContent(layout, new Control[] { close }, new Control[] { again });
        }

        private void AddGroup(StackLayout layout, List<Guid> ids, string title, string counted, string description,
            bool withFixes = false)
        {
            if (ids.Count == 0) return;

            var select = new KitButton($"Select {ids.Count}", ButtonKind.Secondary, KitIcon.Pick)
            {
                Tip = "Select these volumes in Rhino and close this window",
            };
            select.Click += (s, e) =>
            {
                _model.Select(_doc, ids);
                Close(false);
            };
            StackLayout buttons = KitForm.HStack(select);

            if (withFixes)
            {
                var flatten = new KitButton("Flatten") { Tip = "Rotate each volume level about its bottom face" };
                flatten.Click += (s, e) =>
                {
                    int n = _model.Flatten(_doc, ids);
                    RhinoApp.WriteLine($"{GfaInfo.Name}: flattened {n} volume(s).");
                    Build();
                };
                var ignore = new KitButton("Ignore", ButtonKind.Ghost) { Tip = "Keep them as they are and stop flagging them" };
                ignore.Click += (s, e) =>
                {
                    _model.IgnoreTilt(_doc, ids);
                    Build();
                };
                buttons.Items.Add(flatten);
                buttons.Items.Add(ignore);
            }

            // Title with the warning icon, so the status never relies on colour alone.
            StackLayout heading = KitForm.HStack(new KitIconView(KitIcon.Warning, c => c.Warn), KitForm.Label(title), KitForm.Caption(counted));
            layout.Items.Add(KitForm.VStack(6, heading, KitForm.Body(description, TextWidth, secondary: true), buttons));
        }
    }
}
