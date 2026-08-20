using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Numbat.Commands.Modelling.NumbatTemplate
{
    public class NumbatTemplateCommand : Command
    {
        public override string EnglishName => "nbTemplate";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var gp = new GetPoint();
		gp.SetCommandPrompt("Pick the top-left corner for the Numbat template layer guide");
		gp.SetDefaultPoint(Rhino.Geometry.Point3d.Origin);
		GetResult result = gp.Get();

            if (result != GetResult.Point)
                return Result.Cancel;

            return TemplateBuilder.Create(doc, gp.Point());
        }
    }
}
