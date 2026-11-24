using System;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Numbat.Commands.Analysis.NumbatAf
{
    /// <summary>Opens the floating nbAF window (Abstandsflächen), or brings it to the front.</summary>
    public class NumbatAfCommand : Command
    {
        public override string EnglishName => "nbAF";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            AfWindow.ShowWindow(NumbatPlugin.Instance.AfModel, doc);
            return Result.Success;
        }
    }

    /// <summary>Starts tracking volumes. Used by the window's Add button when nothing is selected.</summary>
    // Hidden: works when typed in full, but doesn't appear in autocomplete, so typing "nb" only offers nbAF.
    [CommandStyle(Style.Hidden)]
    public class NumbatAfAddCommand : Command
    {
        public override string EnglishName => "nbAFAdd";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select storey volumes to add (touching or grouped volumes form one building)");
            go.GeometryFilter = ObjectType.Brep | ObjectType.Extrusion;
            go.GeometryAttributeFilter = GeometryAttributeFilter.ClosedPolysrf;
            go.SubObjectSelect = false;
            go.GroupSelect = true;
            if (go.GetMultiple(1, 0) != GetResult.Object) return go.CommandResult();

            AfModel model = NumbatPlugin.Instance.AfModel;
            model.Refresh(doc);
            int n = model.Add(doc, go.Objects().Select(o => o.Object()).Where(o => o != null));
            RhinoApp.WriteLine($"{AfInfo.Name}: added {n} volume(s). Run nbAF to see the Abstandsflächen.");
            return Result.Success;
        }
    }

    /// <summary>Sets a building's plot boundary: nbAFSetPlot B2. Used by the window's plot button.</summary>
    // Hidden: works when typed in full, but doesn't appear in autocomplete, so typing "nb" only offers nbAF.
    [CommandStyle(Style.Hidden)]
    public class NumbatAfSetPlotCommand : Command
    {
        public override string EnglishName => "nbAFSetPlot";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            AfModel model = NumbatPlugin.Instance.AfModel;
            model.Refresh(doc);
            if (model.Buildings.Count == 0)
            {
                RhinoApp.WriteLine($"{AfInfo.Name}: add buildings first (nbAF, then Add).");
                return Result.Nothing;
            }

            string name = model.Buildings[0].Name;
            if (RhinoGet.GetString("Building (B1, B2, ...)", false, ref name) != Result.Success) return Result.Cancel;
            Building b = model.Find(name);
            if (b == null)
            {
                RhinoApp.WriteLine($"{AfInfo.Name}: no building called {name}.");
                return Result.Failure;
            }

            var go = new GetObject();
            go.SetCommandPrompt($"Select the plot boundary of {b.Name} (closed curve)");
            go.GeometryFilter = ObjectType.Curve;
            go.GeometryAttributeFilter = GeometryAttributeFilter.ClosedCurve;
            go.SubObjectSelect = false;
            if (go.Get() != GetResult.Object) return go.CommandResult();

            model.SetPlot(doc, b, go.Object(0).Object());
            RhinoApp.WriteLine($"{AfInfo.Name}: plot of {b.Name} set.");
            return Result.Success;
        }
    }

    /// <summary>Picks curves to use as road centre lines. Used by the window's road Add button when nothing is selected.</summary>
    // Hidden: works when typed in full, but doesn't appear in autocomplete, so typing "nb" only offers nbAF.
    [CommandStyle(Style.Hidden)]
    public class NumbatAfAddRoadCommand : Command
    {
        public override string EnglishName => "nbAFAddRoad";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select road centre lines (Abstandsflächen may reach a road only up to its middle)");
            go.GeometryFilter = ObjectType.Curve;
            go.SubObjectSelect = false;
            if (go.GetMultiple(1, 0) != GetResult.Object) return go.CommandResult();

            AfModel model = NumbatPlugin.Instance.AfModel;
            int n = model.AddRoads(doc, go.Objects().Select(o => o.Object()).Where(o => o != null));
            RhinoApp.WriteLine($"{AfInfo.Name}: {n} road centre line(s) added.");
            return Result.Success;
        }
    }

    /// <summary>
    /// Adds the current Abstandsflächen, footprints, plots, labels and clashes to the nbAF sublayers, replacing what
    /// was baked before. One undo step. Used by the window's Bake button.
    /// </summary>
    // Hidden: works when typed in full, but doesn't appear in autocomplete, so typing "nb" only offers nbAF.
    [CommandStyle(Style.Hidden)]
    public class NumbatAfBakeCommand : Command
    {
        public override string EnglishName => "nbAFBake";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            AfModel model = NumbatPlugin.Instance.AfModel;
            model.Refresh(doc);
            if (model.Buildings.Count == 0 || model.Current == null)
            {
                RhinoApp.WriteLine($"{AfInfo.Name}: nothing to bake. Add buildings in the nbAF window first.");
                return Result.Nothing;
            }

            int removed = AfBake.Clear(doc);
            int added = AfBake.Write(doc, model.Current);
            doc.Views.Redraw();
            if (added < 0)
            {
                RhinoApp.WriteLine($"{AfInfo.Name}: could not create the \"{AfInfo.OutputLayer}\" layers.");
                return Result.Failure;
            }
            RhinoApp.WriteLine($"{AfInfo.Name}: baked {added} object(s) to the \"{AfInfo.OutputLayer}\" layers" +
                               (removed > 0 ? $", replacing {removed}." : "."));
            return Result.Success;
        }
    }

    /// <summary>Deletes everything nbAF baked (the objects on the nbAF sublayers). The buildings stay tracked.</summary>
    // Hidden: works when typed in full, but doesn't appear in autocomplete, so typing "nb" only offers nbAF.
    [CommandStyle(Style.Hidden)]
    public class NumbatAfClearCommand : Command
    {
        public override string EnglishName => "nbAFClear";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (mode == RunMode.Interactive)
            {
                bool yes = false;
                Result r = RhinoGet.GetBool($"Delete everything on the \"{AfInfo.OutputLayer}\" output layers?", false, "No", "Yes", ref yes);
                if (r != Result.Success || !yes) return Result.Cancel;
            }
            int n = AfBake.Clear(doc);
            doc.Views.Redraw();
            RhinoApp.WriteLine($"{AfInfo.Name}: deleted {n} baked object(s).");
            return Result.Success;
        }
    }

    /// <summary>
    /// Lists the legal texts and data sources behind nbAF's rules, with the date each was saved, and opens one:
    /// the copy that ships with Numbat, or the live page.
    /// </summary>
    // Hidden: works when typed in full, but doesn't appear in autocomplete, so typing "nb" only offers nbAF.
    [CommandStyle(Style.Hidden)]
    public class NumbatAfSourcesCommand : Command
    {
        public override string EnglishName => "nbAFSources";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            RhinoApp.WriteLine($"{AfInfo.Name} sources:");
            foreach (AfSource s in AfSources.All)
            {
                RhinoApp.WriteLine($"  {s.Key}: {s.Title}");
                RhinoApp.WriteLine($"      {s.Note}");
                if (s.File != null)
                    RhinoApp.WriteLine($"      Saved copy ({s.Accessed}): {(s.HasLocalCopy ? s.LocalPath : "missing")}");
                RhinoApp.WriteLine($"      Online: {s.Url}");
            }
            if (mode != RunMode.Interactive) return Result.Success;

            var gp = new GetOption();
            gp.SetCommandPrompt("Open a source (Enter to finish)");
            gp.AcceptNothing(true);
            var online = new OptionToggle(false, "Saved", "Online");

            while (true)
            {
                gp.ClearCommandOptions();
                gp.AddOptionToggle("Copy", ref online);
                int first = -1;
                foreach (AfSource s in AfSources.All)
                {
                    int index = gp.AddOption(s.Key);
                    if (first < 0) first = index;
                }

                GetResult res = gp.Get();
                if (res != GetResult.Option) return res == GetResult.Nothing ? Result.Success : Result.Cancel;
                if (gp.Option().EnglishName == "Copy") continue;

                AfSource chosen = AfSources.All[gp.OptionIndex() - first];
                AfSources.Open(online.CurrentValue || !chosen.HasLocalCopy ? chosen.Url : chosen.LocalPath);
            }
        }
    }

    /// <summary>Shows the how-to guide (docs/nbAF.md) and the sources.</summary>
    // Hidden: works when typed in full, but doesn't appear in autocomplete, so typing "nb" only offers nbAF.
    [CommandStyle(Style.Hidden)]
    public class NumbatAfHelpCommand : Command
    {
        public override string EnglishName => "nbAFHelp";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            AfHelp.Show();
            return Result.Success;
        }
    }
}
