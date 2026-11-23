using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using Numbat.Commands.Modelling.NumbatStair.Spiral;
using Numbat.Commands.Modelling.NumbatStair.Straight;

namespace Numbat.Commands.Modelling.NumbatStair
{
    public class NumbatStairCommand : Command
    {
        public static NumbatStairCommand Instance { get; private set; }

        public NumbatStairCommand()
        {
            Instance = this;
        }

        public override string EnglishName => "nbStair";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var getOptions = new GetOption();
            getOptions.SetCommandPrompt("Choose stair type");
            getOptions.AcceptNothing(false);
            getOptions.AddOption("Spiral");
            getOptions.AddOption("Straight");

            while (true)
            {
                var result = getOptions.Get();

                if (result == GetResult.Cancel)
                    return Result.Cancel;

                if (result != GetResult.Option)
                    continue;

                var option = getOptions.Option();
                if (option == null)
                    continue;

                if (option.EnglishName == "Spiral")
                    return NumbatStairSpiralCommand.RunStairSpiral(doc);

                if (option.EnglishName == "Straight")
                    return NumbatStairStraightCommand.RunStairStraight(doc);
            }
        }
    }
}
