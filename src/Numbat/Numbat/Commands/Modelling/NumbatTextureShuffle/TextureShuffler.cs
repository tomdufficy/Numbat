using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Render;

namespace Numbat.Commands.Modelling.NumbatTextureShuffle
{
    /// <summary>What a button in the window does.</summary>
    internal enum ShuffleAction
    {
        /// <summary>"Shuffle": box mapping moved by a new random shift from its starting position.</summary>
        Shift,
        /// <summary>"Straighten": box mapping at its starting position, no random shift.</summary>
        Apply,
        /// <summary>New repeat size, keeping each object's current position (only objects that already have a box
        /// mapping).</summary>
        Resize,
        /// <summary>"Remove mapping": back to Rhino's default surface mapping.</summary>
        Reset,
    }

    /// <summary>
    /// Sets box texture mappings on objects (mapping channel 1, the one materials use by default).
    /// Each call is one undo step.
    /// </summary>
    internal static class TextureShuffler
    {
        public const int Channel = 1;
        private static readonly Random Rng = new Random();

        public static bool IsTarget(RhinoObject obj)
        {
            GeometryBase g = obj?.Geometry;
            return g is Brep || g is Extrusion || g is Surface || g is Mesh || g is SubD;
        }

        /// <summary>Applies <paramref name="action"/> to the objects. Returns how many were changed.</summary>
        public static int Run(RhinoDoc doc, IList<RhinoObject> objects, TextureShuffleSettings s, ShuffleAction action)
        {
            if (doc == null || objects.Count == 0) return 0;
            double mm = TextureShuffleSettings.MmToModel(doc);
            var dx = new Interval(0, Math.Max(s.SizeX * mm, doc.ModelAbsoluteTolerance));
            var dy = new Interval(0, Math.Max(s.SizeY * mm, doc.ModelAbsoluteTolerance));
            var dz = new Interval(0, Math.Max(s.SizeZ * mm, doc.ModelAbsoluteTolerance));
            double reach = Math.Max(0, s.MaxOffset * mm);

            // Same for all: one shift shared by every object, so the pattern stays continuous across them.
            Vector3d sharedShift = RandomShift(s, reach);

            // Inside a command Rhino already records undo; from the window each click is its own undo step.
            uint undo = doc.UndoRecordingIsActive ? 0 : doc.BeginUndoRecord($"nbTextureShuffle: {action}");
            int changed = 0;
            try
            {
                foreach (RhinoObject obj in objects.Where(IsTarget))
                {
                    if (action == ShuffleAction.Resize && !TryGetMappingBox(obj, out Box _)) continue;
                    TextureMapping mapping;
                    if (action == ShuffleAction.Reset)
                    {
                        mapping = TextureMapping.CreateSurfaceParameterMapping();
                    }
                    else
                    {
                        Plane basePlane = BasePlane(obj, s.AlignToObject, doc.ModelAbsoluteTolerance);
                        Plane plane = action == ShuffleAction.Resize ? CurrentPlane(obj, basePlane) : basePlane;
                        if (action == ShuffleAction.Shift)
                        {
                            Vector3d shift = s.SameForAll ? sharedShift : RandomShift(s, reach);
                            plane.Origin = basePlane.Origin + basePlane.XAxis * shift.X + basePlane.YAxis * shift.Y +
                                           basePlane.ZAxis * shift.Z;
                        }
                        mapping = TextureMapping.CreateBoxMapping(plane, dx, dy, dz, true);
                    }

                    if (mapping != null && doc.Objects.ModifyTextureMapping(obj, Channel, mapping)) changed++;
                }
            }
            finally
            {
                if (undo != 0) doc.EndUndoRecord(undo);
            }
            doc.Views.Redraw();
            return changed;
        }

        /// <summary>The object's current box mapping as a box (one texture repeat), if it has one.</summary>
        public static bool TryGetMappingBox(RhinoObject obj, out Box box)
        {
            box = Box.Unset;
            TextureMapping existing = obj?.GetTextureMapping(Channel);
            if (existing == null || existing.MappingType != TextureMappingType.BoxMapping) return false;
            if (!existing.TryGetMappingBox(out Plane plane, out Interval dx, out Interval dy, out Interval dz)) return false;
            box = new Box(plane, dx, dy, dz);
            return box.IsValid;
        }

        private static Vector3d RandomShift(TextureShuffleSettings s, double reach)
        {
            double R() => (Rng.NextDouble() * 2 - 1) * reach;
            return new Vector3d(s.OffsetX ? R() : 0, s.OffsetY ? R() : 0, s.OffsetZ ? R() : 0);
        }

        /// <summary>
        /// The object's current box mapping plane, if it has one lying the same way up as
        /// <paramref name="basePlane"/>; otherwise the base plane.
        /// </summary>
        private static Plane CurrentPlane(RhinoObject obj, Plane basePlane)
        {
            TextureMapping existing = obj.GetTextureMapping(Channel);
            if (existing != null &&
                existing.TryGetMappingBox(out Plane plane, out Interval _, out Interval _, out Interval _) &&
                plane.IsValid &&
                plane.ZAxis.IsParallelTo(basePlane.ZAxis, RhinoMath.ToRadians(1)) == 1)
            {
                return plane;
            }
            return basePlane;
        }

        /// <summary>
        /// World: the world XY plane, so neighbouring objects line up. Object: the plane of the object's largest
        /// flat face (falling back to world axes through its bounding box corner).
        /// </summary>
        private static Plane BasePlane(RhinoObject obj, bool alignToObject, double tolerance)
        {
            if (!alignToObject) return Plane.WorldXY;

            BoundingBox box = obj.Geometry.GetBoundingBox(true);
            var fallback = new Plane(box.Min, Vector3d.XAxis, Vector3d.YAxis);

            Brep brep = obj.Geometry as Brep;
            if (brep == null && obj.Geometry is Extrusion ext) brep = ext.ToBrep(false);
            if (brep == null && obj.Geometry is Surface srf) brep = srf.ToBrep();
            if (brep == null) return fallback;

            Plane best = Plane.Unset;
            double bestArea = 0;
            foreach (BrepFace face in brep.Faces)
            {
                if (!face.TryGetPlane(out Plane plane, tolerance)) continue;
                using (Brep single = face.DuplicateFace(false))
                {
                    double area = AreaMassProperties.Compute(single, true, false, false, false)?.Area ?? 0;
                    if (area > bestArea)
                    {
                        bestArea = area;
                        best = plane;
                    }
                }
            }
            if (!best.IsValid) return fallback;

            // Keep the plane's X axis as level as possible so textures don't start out skewed.
            Vector3d x = Vector3d.CrossProduct(Vector3d.ZAxis, best.ZAxis);
            if (!x.Unitize()) x = Vector3d.XAxis;
            return new Plane(box.Min, x, Vector3d.CrossProduct(best.ZAxis, x));
        }
    }
}
