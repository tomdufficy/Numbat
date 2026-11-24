using System.Linq;
using Eto.Forms;
using Rhino;
using Numbat.UI;

namespace Numbat.Commands.Analysis.NumbatAf
{
    /// <summary>
    /// Lists buildings that almost touch, closest first, and lets the user decide for each pair: Join (one building,
    /// by grouping the two closest volumes), Keep apart (separate buildings; nbAF stops asking) or Select (look at
    /// them). The list updates after every choice.
    /// </summary>
    internal sealed class NearMissDialog : KitDialog<bool>
    {
        private const int TextWidth = 440;

        private readonly AfModel _model;
        private readonly RhinoDoc _doc;
        private readonly StackLayout _list = new StackLayout { Spacing = 8, HorizontalContentAlignment = HorizontalAlignment.Stretch };

        private NearMissDialog(AfModel model, RhinoDoc doc) : base($"{AfInfo.Name} – Almost touching")
        {
            _model = model;
            _doc = doc;
            var close = new KitButton("Close", ButtonKind.Primary);
            close.Click += (s, e) => Close(true);
            var joinAll = new KitButton("Join all", ButtonKind.Ghost) { Tip = "Make every pair in the list one building" };
            joinAll.Click += (s, e) =>
            {
                foreach (NearMiss m in _model.NearMisses.ToList()) _model.JoinBuildings(_doc, m);
                Fill();
            };
            AcceptButton = close;
            CancelButton = close;

            var body = new StackLayout
            {
                Spacing = 12,
                Width = TextWidth,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items =
                {
                    KitForm.Body($"These buildings are closer than {AfRules.NearMissM * 100:0} cm but don't touch, so nbAF counts them as " +
                                 "separate buildings, and their Abstandsflächen may clash with each other. If a pair is meant as one " +
                                 "building, join it.", TextWidth, secondary: true),
                    new Scrollable { Content = _list, Border = BorderType.None, Height = 300, BackgroundColor = Kit.Colours.Surface },
                },
            };
            SetContent(body, new Control[] { close }, new Control[] { joinAll });
            Fill();
        }

        public static void Show(Window owner, AfModel model, RhinoDoc doc)
        {
            if (doc == null) return;
            Display.Load();
            new NearMissDialog(model, doc).ShowModal(owner);
        }

        private void Fill()
        {
            _list.Items.Clear();
            if (_model.NearMisses.Count == 0)
            {
                _list.Items.Add(KitForm.Body("Nothing left to check: no buildings are almost touching.", TextWidth));
                return;
            }
            foreach (NearMiss m in _model.NearMisses)
            {
                NearMiss pair = m;
                var select = new KitButton("Select", ButtonKind.Ghost, KitIcon.Pick) { Tip = $"Select the two closest volumes of {m.A.Name} and {m.B.Name}" };
                select.Click += (s, e) =>
                {
                    _doc.Objects.UnselectAll();
                    _doc.Objects.Select(pair.VolumeA);
                    _doc.Objects.Select(pair.VolumeB);
                    _doc.Views.Redraw();
                };
                var apart = new KitButton("Keep apart") { Tip = "They are separate buildings: stop asking" };
                apart.Click += (s, e) =>
                {
                    _model.KeepApart(_doc, pair);
                    Fill();
                };
                var join = new KitButton("Join") { Tip = "They are one building: group the two closest volumes" };
                join.Click += (s, e) =>
                {
                    _model.JoinBuildings(_doc, pair);
                    Fill();
                };
                var row = new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Items = { new StackLayoutItem(KitForm.Label($"{m.A.Name} and {m.B.Name} · {m.GapText} apart"), true), select, apart, join },
                };
                _list.Items.Add(row);
            }
        }
    }
}
