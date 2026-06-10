using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input.Custom;

namespace Numbat.Commands.Modelling
{
    public class NumbatTextureShuffleCommand : Command
    {
        public static NumbatTextureShuffleCommand Instance { get; private set; }

        public NumbatTextureShuffleCommand()
        {
            Instance = this;
        }

        public override string EnglishName => "nbTextureShuffle";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select objects to texture shuffle");
            go.GeometryFilter =
                ObjectType.Surface |
                ObjectType.PolysrfFilter |
                ObjectType.Mesh;

            go.EnablePreSelect(true, true);
            go.GetMultiple(1, 0);

            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            var createFreshMapping = new OptionToggle(true, "KeepExisting", "CreateFresh");
            var sameMappingForAll = new OptionToggle(true, "PerObject", "SameForAll");
            var mappingType = new OptionToggle(true, "WorldXYZBox", "ObjectNormalBox");
            var xSize = new OptionDouble(250.0, true, 0.001);
            var ySize = new OptionDouble(250.0, true, 0.001);
            var zSize = new OptionDouble(250.0, true, 0.001);
            var maxOffset = new OptionDouble(250.0, true, 0.0);

            var directionIndex = 3;
            string[] directionOptions = { "X", "Y", "Z", "All" };

            var getOptions = new GetOption();
            getOptions.SetCommandPrompt("Texture shuffle options");

            getOptions.AddOptionToggle("Mapping", ref createFreshMapping);
            getOptions.AddOptionToggle("Objects", ref sameMappingForAll);
            getOptions.AddOptionToggle("BoxType", ref mappingType);
            getOptions.AddOptionDouble("XSize", ref xSize);
            getOptions.AddOptionDouble("YSize", ref ySize);
            getOptions.AddOptionDouble("ZSize", ref zSize);
            getOptions.AddOptionList("RandomDirection", directionOptions, directionIndex);
            getOptions.AddOptionDouble("MaxOffset", ref maxOffset);

            var optionResult = getOptions.Get();

            if (optionResult == Rhino.Input.GetResult.Cancel)
                return Result.Cancel;

            if (optionResult == Rhino.Input.GetResult.Option)
            {
                var option = getOptions.Option();

                if (option != null && option.Index == 7)
                    directionIndex = option.CurrentListOptionIndex;
            }

            RhinoApp.WriteLine($"Objects selected: {go.ObjectCount}");
            RhinoApp.WriteLine($"Mapping: {(createFreshMapping.CurrentValue ? "Create fresh mapping" : "Keep existing mapping")}");
            RhinoApp.WriteLine($"Object mode: {(sameMappingForAll.CurrentValue ? "Same mapping for all objects" : "Per-object mapping")}");
            RhinoApp.WriteLine($"Box type: {(mappingType.CurrentValue ? "Object normal box" : "World XYZ box")}");
            RhinoApp.WriteLine($"Mapping size: X={xSize.CurrentValue}, Y={ySize.CurrentValue}, Z={zSize.CurrentValue}");
            RhinoApp.WriteLine($"Random direction: {directionOptions[directionIndex]}");
            RhinoApp.WriteLine($"Max offset: {maxOffset.CurrentValue}");

            return Result.Success;
        }
    }
}