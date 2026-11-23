using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Numbat.Commands.Analysis.NumbatGfa
{
    /// <summary>Opens the floating GFA window, or brings it to the front.</summary>
    public class NumbatGfaCommand : Command
    {
        public override string EnglishName => "nbGFA";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            GfaWindow.ShowWindow(NumbatPlugin.Instance.GfaModel);
            return Result.Success;
        }
    }

    /// <summary>Checks assigned volumes for problems (open, tilted, no flat bottom, overlaps).</summary>
    public class NumbatGfaCheckCommand : Command
    {
        public override string EnglishName => "nbGFACheck";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            IssuesDialog.Show(Rhino.UI.RhinoEtoApp.MainWindow, NumbatPlugin.Instance.GfaModel, doc);
            return Result.Success;
        }
    }

    /// <summary>Shows the how-to guide (docs/nbGFA.md).</summary>
    public class NumbatGfaHelpCommand : Command
    {
        public override string EnglishName => "nbGFAHelp";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            HelpWindow.ShowWindow();
            return Result.Success;
        }
    }

    /// <summary>Command-line way to assign a use: select volumes, then pick a use.</summary>
    public class NumbatGfaAssignCommand : Command
    {
        public override string EnglishName => "nbGFAAssign";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            GfaModel model = NumbatPlugin.Instance.GfaModel;

            var go = new GetObject();
            go.SetCommandPrompt("Select volumes to assign a use");
            go.GeometryFilter = ObjectType.Brep | ObjectType.Extrusion;
            go.SetCustomGeometryFilter((obj, geometry, index) => Volumes.IsVolume(obj));
            go.GetMultiple(1, 0);
            if (go.CommandResult() != Result.Success) return go.CommandResult();
            List<RhinoObject> objects = go.Objects().Select(r => r.Object()).Where(o => o != null).ToList();

            var gu = new GetOption();
            gu.SetCommandPrompt("Use");
            var byIndex = new Dictionary<int, UseCategory>();
            foreach (UseCategory use in model.Categories)
            {
                byIndex[gu.AddOption(OptionName(use.Name))] = use;
            }
            int unassign = gu.AddOption("Unassign");

            if (gu.Get() != GetResult.Option) return Result.Cancel;
            int picked = gu.Option().Index;

            int n = picked == unassign
                ? model.Unassign(doc, objects)
                : model.Assign(doc, objects, byIndex[picked]);
            RhinoApp.WriteLine($"{GfaInfo.Name}: {n} volume(s) updated.");
            return Result.Success;
        }

        /// <summary>Command-line option names allow only letters, digits and underscores.</summary>
        private static string OptionName(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            if (sb.Length == 0 || !char.IsLetter(sb[0])) sb.Insert(0, 'U');
            return sb.ToString();
        }
    }
}
