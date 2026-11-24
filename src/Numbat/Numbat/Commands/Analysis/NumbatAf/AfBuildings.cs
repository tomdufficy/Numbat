using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace Numbat.Commands.Analysis.NumbatAf
{
    /// <summary>Two volumes in different buildings that almost touch: probably meant as one building, or not.</summary>
    internal sealed class VolumePair
    {
        public int A, B;      // volume indices
        public double Gap;    // model units
    }

    /// <summary>
    /// Sorts volumes into buildings. Models are stacked volumes, one closed box (or roof shape) per storey or part.
    /// Volumes that touch, overlap or are closer than the join gap belong to the same building, and so do volumes in
    /// the same Rhino group (which lets users join parts that are further apart). Pairs the user marked "keep apart"
    /// are never joined by distance. Volumes of different buildings that are closer than the near-miss limit are
    /// reported, so the user can decide whether they are one building.
    /// </summary>
    internal static class AfBuildings
    {
        /// <param name="joinGap">Volumes closer than this (model units) touch.</param>
        /// <param name="nearLimit">Report pairs of different buildings closer than this (model units).</param>
        /// <param name="keepApart">Volume index pairs the user said are separate.</param>
        /// <returns>Lists of volume indices, one list per building.</returns>
        public static List<List<int>> Group(IList<Brep> volumes, IList<int[]> groups, double joinGap, double nearLimit,
            double tol, ICollection<(int, int)> keepApart, out List<VolumePair> nearMisses)
        {
            int n = volumes.Count;
            var parent = Enumerable.Range(0, n).ToArray();
            int Root(int i) => parent[i] == i ? i : parent[i] = Root(parent[i]);
            void Join(int a, int b) => parent[Root(a)] = Root(b);

            double reach = Math.Max(joinGap, nearLimit);
            var boxes = volumes.Select(v => { BoundingBox b = v.GetBoundingBox(true); b.Inflate(reach + tol * 2); return b; }).ToList();
            var close = new List<VolumePair>();
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (!Overlap(boxes[i], boxes[j])) continue;
                    double gap = Gap(volumes[i], volumes[j], tol, reach);
                    if (gap > reach) continue;
                    bool apart = keepApart != null && (keepApart.Contains((i, j)) || keepApart.Contains((j, i)));
                    if (gap <= joinGap && !apart) Join(i, j);
                    else if (!apart) close.Add(new VolumePair { A = i, B = j, Gap = gap });
                }
            }

            if (groups != null)
            {
                var firstInGroup = new Dictionary<int, int>();
                for (int i = 0; i < n && i < groups.Count; i++)
                {
                    foreach (int g in groups[i] ?? new int[0])
                    {
                        if (firstInGroup.TryGetValue(g, out int other)) Join(i, other);
                        else firstInGroup[g] = i;
                    }
                }
            }

            // Near-misses between what ended up as different buildings: the closest pair per pair of buildings.
            nearMisses = close
                .Where(p => Root(p.A) != Root(p.B))
                .GroupBy(p => (Math.Min(Root(p.A), Root(p.B)), Math.Max(Root(p.A), Root(p.B))))
                .Select(g => g.OrderBy(p => p.Gap).First())
                .ToList();

            return Enumerable.Range(0, n).GroupBy(Root).Select(g => g.ToList()).ToList();
        }

        /// <summary>
        /// The gap between two volumes, or 0 when they touch or overlap. Measured from the corners of each to the
        /// other's surface, which is exact for the box-like volumes of a massing model. Stops early (returns more than
        /// <paramref name="reach"/>) when they are clearly apart.
        /// </summary>
        public static double Gap(Brep a, Brep b, double tol, double reach)
        {
            double best = double.MaxValue;
            foreach (BrepVertex v in a.Vertices) best = Math.Min(best, b.ClosestPoint(v.Location).DistanceTo(v.Location));
            foreach (BrepVertex v in b.Vertices) best = Math.Min(best, a.ClosestPoint(v.Location).DistanceTo(v.Location));
            if (best <= tol * 2) return 0;
            if (a.Vertices.Any(v => b.IsPointInside(v.Location, tol, false)) || b.Vertices.Any(v => a.IsPointInside(v.Location, tol, false))) return 0;
            if (best > reach && !Overlap(a.GetBoundingBox(true), b.GetBoundingBox(true))) return best;
            // Crossing without a corner inside the other (e.g. two bars in a plus shape).
            if (Intersection.BrepBrep(a, b, tol, out Curve[] curves, out Point3d[] points) &&
                ((curves?.Length ?? 0) > 0 || (points?.Length ?? 0) > 0)) return 0;
            return best;
        }

        private static bool Overlap(BoundingBox a, BoundingBox b) =>
            a.Min.X <= b.Max.X && b.Min.X <= a.Max.X &&
            a.Min.Y <= b.Max.Y && b.Min.Y <= a.Max.Y &&
            a.Min.Z <= b.Max.Z && b.Min.Z <= a.Max.Z;
    }
}
