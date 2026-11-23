using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Numbat.Commands.Modelling.NumbatTextureShuffle
{
    /// <summary>
    /// Opens the texture shuffle window. Scripted (-nbTextureShuffle): select objects and shuffle once with the
    /// saved settings, so it also works in macros.
    /// </summary>
    public class NumbatTextureShuffleCommand : Command
    {
        public override string EnglishName => "nbTextureShuffle";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (mode == RunMode.Interactive)
            {
                TextureShuffleWindow.ShowWindow();
                return Result.Success;
            }

            var go = new GetObject();
            go.SetCommandPrompt("Select objects to shuffle textures on");
            go.GeometryFilter = ObjectType.Surface | ObjectType.PolysrfFilter | ObjectType.Extrusion |
                                ObjectType.Mesh | ObjectType.SubD;
            go.EnablePreSelect(true, true);
            go.GetMultiple(1, 0);
            if (go.CommandResult() != Result.Success) return go.CommandResult();

            List<RhinoObject> objects = go.Objects().Select(r => r.Object()).Where(o => o != null).ToList();
            var gs = new GetOption();
            gs.SetCommandPrompt("Press Enter to shuffle");
            int straighten = gs.AddOption("Straighten");
            int remove = gs.AddOption("RemoveMapping");
            gs.AcceptNothing(true);
            GetResult result = gs.Get();
            if (result == GetResult.Cancel) return Result.Cancel;

            ShuffleAction action = ShuffleAction.Shift;
            if (result == GetResult.Option)
            {
                int i = gs.Option().Index;
                action = i == straighten ? ShuffleAction.Apply : i == remove ? ShuffleAction.Reset : ShuffleAction.Shift;
            }

            int n = TextureShuffler.Run(doc, objects, TextureShuffleSettings.Load(), action);
            RhinoApp.WriteLine($"nbTextureShuffle: {n} object(s) changed.");
            return Result.Success;
        }
    }
}
