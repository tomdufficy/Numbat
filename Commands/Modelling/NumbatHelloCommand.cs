using Rhino;
using Rhino.Commands;

namespace Numbat.Commands.Modelling
{
    public class NumbatHelloCommand : Command
    {
        public NumbatHelloCommand()
        {
            Instance = this;
        }

        public static NumbatHelloCommand Instance { get; private set; }

        public override string EnglishName => "NumbatHello";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            RhinoApp.WriteLine("Numbat loaded successfully.");
            return Result.Success;
        }
    }
}