using Rhino;
using Rhino.Commands;

namespace Numbat.Commands.Utilities.NumbatAnon
{
    /// <summary>Opens the nbAnon window: find a phrase everywhere in the file and anonymise it.</summary>
    public class NumbatAnonCommand : Command
    {
        public override string EnglishName => "nbAnon";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            AnonWindow.ShowWindow();
            return Result.Success;
        }
    }

    /// <summary>Shows the nbAnon guide (docs/nbAnon.md).</summary>
    // Hidden: works when typed in full, but doesn't appear in autocomplete, so typing "nb" only offers nbAnon.
    [CommandStyle(Style.Hidden)]
    public class NumbatAnonHelpCommand : Command
    {
        public override string EnglishName => "nbAnonHelp";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            AnonHelp.Show();
            return Result.Success;
        }
    }
}
