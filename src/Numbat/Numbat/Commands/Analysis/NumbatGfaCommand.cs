using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;

namespace Numbat.Commands.Analysis
{
    public class NumbatGfaCommand : Command
    {
        public static NumbatGfaCommand Instance { get; private set; }

        public NumbatGfaCommand()
        {
            Instance = this;
        }

        public override string EnglishName => "nbGFA";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select closed polysurfaces");
            go.GeometryFilter = ObjectType.Brep;
            go.GetMultiple(1, 0);

            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            var items = new List<GfaItem>();
            var issueIds = new List<Guid>();

            for (int i = 0; i < go.ObjectCount; i++)
            {
                var objRef = go.Object(i);
                var rhinoObject = objRef.Object();
                var brep = objRef.Brep();

                if (rhinoObject == null || brep == null || !brep.IsSolid)
                    continue;

                var bottomFace = FindBottomFace(brep);

                if (bottomFace == null)
                    continue;

                bool isValidBottom = IsHorizontalPlanarFace(bottomFace, doc.ModelAbsoluteTolerance);

                if (!isValidBottom)
                    issueIds.Add(rhinoObject.Id);

                double area = AreaMassProperties.Compute(bottomFace)?.Area ?? 0.0;

                items.Add(new GfaItem
                {
                    Id = rhinoObject.Id,
                    Brep = brep,
                    BottomFace = bottomFace,
                    BottomArea = area,
                    HasIssue = !isValidBottom
                });
            }

            if (items.Count == 0)
            {
                RhinoApp.WriteLine("No valid closed polysurfaces selected.");
                return Result.Nothing;
            }

            FlagContainedVolumes(items, issueIds, doc.ModelAbsoluteTolerance);

            if (issueIds.Count > 0)
            {
                RhinoApp.WriteLine(
                    $"{issueIds.Count} selected volume(s) have invalid bottom faces or are contained within another selected volume."
                );

                doc.Objects.UnselectAll();

                foreach (var id in issueIds)
                    doc.Objects.Select(id);

                doc.Views.Redraw();

                RhinoApp.WriteLine("Problematic volume(s) highlighted.");
                return Result.Cancel;
            }

            double totalArea = 0.0;

            foreach (var item in items)
                totalArea += item.BottomArea;

            double totalAreaSquareMetres = ConvertAreaToSquareMetres(totalArea, doc.ModelUnitSystem);

            RhinoApp.WriteLine($"Total GFA: {totalAreaSquareMetres:F2} m²");

            Rhino.UI.Dialogs.ShowMessage(
                $"Total GFA: {totalAreaSquareMetres:F2} m²",
                "nbGFA"
            );

            return Result.Success;
        }

        private static BrepFace FindBottomFace(Brep brep)
        {
            BrepFace bottomFace = null;
            double lowestZ = double.MaxValue;

            foreach (var face in brep.Faces)
            {
                var u = (face.Domain(0).Min + face.Domain(0).Max) * 0.5;
                var v = (face.Domain(1).Min + face.Domain(1).Max) * 0.5;
                var centre = face.PointAt(u, v);

                if (centre.Z < lowestZ)
                {
                    lowestZ = centre.Z;
                    bottomFace = face;
                }
            }

            return bottomFace;
        }

        private static bool IsHorizontalPlanarFace(BrepFace face, double tolerance)
        {
            if (!face.IsPlanar(tolerance))
                return false;

            if (!face.TryGetPlane(out Plane plane, tolerance))
                return false;

            double zAlignment = Math.Abs(plane.Normal * Vector3d.ZAxis);

            return Math.Abs(zAlignment - 1.0) <= 0.001;
        }

        private static void FlagContainedVolumes(List<GfaItem> items, List<Guid> issueIds, double tolerance)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var testPoint = items[i].Brep.GetBoundingBox(true).Center;

                for (int j = 0; j < items.Count; j++)
                {
                    if (i == j)
                        continue;

                    if (items[j].Brep.IsPointInside(testPoint, tolerance, true))
                    {
                        if (!issueIds.Contains(items[i].Id))
                            issueIds.Add(items[i].Id);

                        items[i].HasIssue = true;
                        break;
                    }
                }
            }
        }

        private static double ConvertAreaToSquareMetres(double area, UnitSystem unitSystem)
        {
            double scale = RhinoMath.UnitScale(unitSystem, UnitSystem.Meters);
            return area * scale * scale;
        }

        private class GfaItem
        {
            public Guid Id { get; set; }
            public Brep Brep { get; set; }
            public BrepFace BottomFace { get; set; }
            public double BottomArea { get; set; }
            public bool HasIssue { get; set; }
        }
    }
}