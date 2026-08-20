using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.UI;

namespace Numbat.Commands.Analysis.NumbatProgramBlocks
{
    public class NumbatProgramBlocksCommand : Command
    {
        public override string EnglishName => "nbProgramBlocks";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (!ProgramBlocksBuilder.TryGetMetresPerDocumentUnit(doc, out double metresPerUnit, out string unitError))
            {
                Dialogs.ShowMessage(unitError, "nbProgramBlocks");
                return Result.Failure;
            }

            var dialog = new ProgramBlocksDialog();
            bool accepted = dialog.ShowModal(RhinoEtoApp.MainWindow);
            if (!accepted || dialog.Rows == null || dialog.Options == null)
                return Result.Cancel;

            var getPoint = new GetPoint();
            getPoint.SetCommandPrompt("Click lower-left placement point, or press Enter for 0,0,0");
            getPoint.AcceptNothing(true);
            GetResult pointResult = getPoint.Get();

            Point3d basePoint;
            if (pointResult == GetResult.Nothing)
                basePoint = Point3d.Origin;
            else if (pointResult == GetResult.Point)
                basePoint = getPoint.Point();
            else
                return getPoint.CommandResult();

            return ProgramBlocksBuilder.Create(
                doc,
                dialog.Rows,
                dialog.Options,
                basePoint,
                metresPerUnit);
        }
    }
}
