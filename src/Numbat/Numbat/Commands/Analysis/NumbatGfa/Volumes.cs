using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Numbat.Commands.Analysis.NumbatGfa
{
    public enum VolumeStatus
    {
        /// <summary>Closed, with a level bottom face. Counted.</summary>
        Ok,
        /// <summary>Closed, bottom face is a little off level (up to <see cref="Volumes.MaxTiltDegrees"/>). Counted, flagged.</summary>
        Tilted,
        /// <summary>Open polysurface. Not counted.</summary>
        NotClosed,
        /// <summary>Closed, but no planar bottom face within the tilt limit. Not counted.</summary>
        NoFlatBottom,
        /// <summary>Not a Brep or Extrusion (mesh, curve, ...). Not counted.</summary>
        NotSupported,
    }

    /// <summary>Result of analysing one object. Areas are in model units squared.</summary>
    public sealed class VolumeInfo
    {
        public VolumeStatus Status { get; internal set; }
        public double Area { get; internal set; }
        public double TiltDegrees { get; internal set; }

        /// <summary>Centre of the bottom face; rotation pivot for flattening.</summary>
        public Point3d BottomCentroid { get; internal set; }

        /// <summary>Bottom face normal, pointing up (Z >= 0).</summary>
        public Vector3d BottomNormal { get; internal set; }

        public Interval ZRange { get; internal set; }

        /// <summary>Bottom face outlines; only filled when requested (for overlap checks).</summary>
        public List<Curve> Outlines { get; } = new List<Curve>();

        public bool Counted => Status == VolumeStatus.Ok || Status == VolumeStatus.Tilted;
    }

    /// <summary>
    /// Geometry rules. A "volume" is a closed Brep or Extrusion representing one storey;
    /// its GFA is the area of its bottom face(s).
    /// </summary>
    public static class Volumes
    {
        /// <summary>Bottom faces within this angle of level count as flat.</summary>
        public const double FlatDegrees = 0.1;

        /// <summary>Bottom faces up to this angle are counted but flagged as tilted; steeper ones are not counted.</summary>
        public const double MaxTiltDegrees = 5;

        public static bool IsVolume(RhinoObject obj)
        {
            switch (obj?.Geometry)
            {
                case Brep brep: return brep.IsSolid;
                case Extrusion ext: return ext.IsSolid;
                default: return false;
            }
        }

        /// <summary>True for geometry nbGFA looks at at all (Breps and Extrusions, closed or not).</summary>
        public static bool IsCandidate(RhinoObject obj) => obj?.Geometry is Brep || obj?.Geometry is Extrusion;

        public static VolumeInfo Analyze(RhinoObject obj, double tolerance, bool withOutlines = false)
        {
            var info = new VolumeInfo();
            Brep brep;
            bool ownsBrep = false;
            switch (obj?.Geometry)
            {
                case Brep b:
                    brep = b;
                    break;
                case Extrusion e:
                    brep = e.ToBrep(false);
                    ownsBrep = true;
                    break;
                default:
                    info.Status = VolumeStatus.NotSupported;
                    return info;
            }
            if (brep == null)
            {
                info.Status = VolumeStatus.NotSupported;
                return info;
            }

            try
            {
                if (!brep.IsSolid)
                {
                    info.Status = VolumeStatus.NotClosed;
                    return info;
                }

                BoundingBox bb = brep.GetBoundingBox(true);
                info.ZRange = new Interval(bb.Min.Z, bb.Max.Z);
                double planarTol = Math.Max(tolerance * 10, 1e-6);
                double minUpZ = Math.Cos(RhinoMath.ToRadians(MaxTiltDegrees));

                // Candidate floor faces: planar and within the tilt limit of level. The lowest one is the floor.
                var candidates = new List<(BrepFace face, Vector3d up, Point3d centroid, double area)>();
                foreach (BrepFace face in brep.Faces)
                {
                    if (!face.TryGetPlane(out Plane plane, planarTol)) continue;
                    Vector3d up = plane.ZAxis;
                    up.Unitize();
                    if (up.Z < 0) up.Reverse();
                    if (up.Z < minUpZ) continue;

                    using (Brep single = face.DuplicateFace(false))
                    using (AreaMassProperties amp = AreaMassProperties.Compute(single, true, true, false, false))
                    {
                        if (amp == null || amp.Area <= 0) continue;
                        candidates.Add((face, up, amp.Centroid, amp.Area));
                    }
                }

                if (candidates.Count == 0)
                {
                    info.Status = VolumeStatus.NoFlatBottom;
                    return info;
                }

                var bottom = candidates.OrderBy(c => c.centroid.Z).First();
                var floorPlane = new Plane(bottom.centroid, bottom.up);

                // Include other faces in the same plane (a floor split into several faces).
                var floorFaces = candidates.Where(c =>
                    Math.Abs(floorPlane.DistanceTo(c.centroid)) <= planarTol &&
                    c.up.IsParallelTo(bottom.up, RhinoMath.ToRadians(0.5)) == 1).ToList();

                info.Area = floorFaces.Sum(c => c.area);
                info.BottomCentroid = bottom.centroid;
                info.BottomNormal = bottom.up;
                info.TiltDegrees = RhinoMath.ToDegrees(Vector3d.VectorAngle(bottom.up, Vector3d.ZAxis));
                info.Status = info.TiltDegrees <= FlatDegrees ? VolumeStatus.Ok : VolumeStatus.Tilted;

                if (withOutlines)
                {
                    foreach (var f in floorFaces)
                    {
                        Curve loop = f.face.OuterLoop?.To3dCurve();
                        if (loop != null) info.Outlines.Add(loop);
                    }
                }
                return info;
            }
            finally
            {
                if (ownsBrep) brep.Dispose();
            }
        }

        /// <summary>Rotation that levels a volume about its bottom face centre.</summary>
        public static Transform FlattenTransform(VolumeInfo info) =>
            Transform.Rotation(info.BottomNormal, Vector3d.ZAxis, info.BottomCentroid);

        /// <summary>Factor that converts model-unit lengths to metres (or feet for imperial models).</summary>
        public static double LengthScale(RhinoDoc doc, out bool imperial)
        {
            imperial = IsImperial(doc.ModelUnitSystem);
            return RhinoMath.UnitScale(doc.ModelUnitSystem, imperial ? UnitSystem.Feet : UnitSystem.Meters);
        }

        /// <summary>
        /// Factor that converts model-unit areas to the display unit. Metric models (mm, cm, m, ...) always
        /// report m². Imperial models are not officially supported but report ft² as a best effort.
        /// </summary>
        public static double AreaScale(RhinoDoc doc, out string unitLabel, out bool imperial)
        {
            double s = LengthScale(doc, out imperial);
            unitLabel = imperial ? "ft²" : "m²";
            return s * s;
        }

        private static bool IsImperial(UnitSystem u) =>
            u == UnitSystem.Inches || u == UnitSystem.Feet || u == UnitSystem.Yards ||
            u == UnitSystem.Miles || u == UnitSystem.Mils || u == UnitSystem.Microinches;
    }
}
