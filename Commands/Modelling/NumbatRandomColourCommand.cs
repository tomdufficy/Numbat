using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input.Custom;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Numbat.Commands.Modelling
{
    public class NumbatRandomColourCommand : Command
    {
        private static readonly Random Random = new Random();

        public override string EnglishName => "nbRandomColour";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var getOption = new GetOption();
            getOption.SetCommandPrompt("Randomize layer colours");
            getOption.AddOption("AllLayersRandom");
            getOption.AddOption("TopLevelWithNestedShades");

            var result = getOption.Get();

            if (result != Rhino.Input.GetResult.Option)
                return Result.Cancel;

            string selectedOption = getOption.Option().EnglishName;

            if (selectedOption == "AllLayersRandom")
                return RandomizeAllLayers(doc);

            if (selectedOption == "TopLevelWithNestedShades")
                return RandomizeTopLevelsWithNestedShades(doc);

            return Result.Cancel;
        }

        private static Result RandomizeAllLayers(RhinoDoc doc)
        {
            int changedCount = 0;

            foreach (Layer layer in doc.Layers)
            {
                if (layer == null || layer.IsDeleted)
                    continue;

                layer.Color = CreateRandomColour();
                doc.Layers.Modify(layer, layer.Index, true);
                changedCount++;
            }

            doc.Views.Redraw();
            RhinoApp.WriteLine("Randomized colours for {0} layer(s).", changedCount);

            return Result.Success;
        }

        private static Result RandomizeTopLevelsWithNestedShades(RhinoDoc doc)
        {
            int changedCount = 0;
            var topLevelColours = new Dictionary<string, Color>();

            foreach (Layer layer in doc.Layers)
            {
                if (layer == null || layer.IsDeleted)
                    continue;

                string topLevelName = GetTopLevelLayerName(layer);
                int layerDepth = GetLayerDepth(layer);

                if (!topLevelColours.ContainsKey(topLevelName))
                    topLevelColours[topLevelName] = CreateRandomColour();

                Color baseColour = topLevelColours[topLevelName];

                layer.Color = layerDepth == 0
                    ? baseColour
                    : CreateShade(baseColour, layerDepth);

                doc.Layers.Modify(layer, layer.Index, true);
                changedCount++;
            }

            doc.Views.Redraw();
            RhinoApp.WriteLine("Randomized colours for {0} layer(s) using top-level layer colour groups.", changedCount);

            return Result.Success;
        }

        private static Color CreateRandomColour()
        {
            return Color.FromArgb(
                Random.Next(256),
                Random.Next(256),
                Random.Next(256)
            );
        }

        private static Color CreateShade(Color baseColour, int depth)
        {
            double amount = Math.Min(0.75, 0.18 + depth * 0.12 + Random.NextDouble() * 0.18);

            int r = Mix(baseColour.R, 255, amount);
            int g = Mix(baseColour.G, 255, amount);
            int b = Mix(baseColour.B, 255, amount);

            return Color.FromArgb(r, g, b);
        }

        private static int Mix(int original, int target, double amount)
        {
            return (int)(original + (target - original) * amount);
        }

        private static string GetTopLevelLayerName(Layer layer)
        {
            string[] parts = layer.FullPath.Split(new[] { "::" }, StringSplitOptions.None);
            return parts[0];
        }

        private static int GetLayerDepth(Layer layer)
        {
            string[] parts = layer.FullPath.Split(new[] { "::" }, StringSplitOptions.None);
            return parts.Length - 1;
        }
    }
}