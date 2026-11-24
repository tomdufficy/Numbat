using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace Numbat.Commands.Analysis.NumbatAf
{
    /// <summary>
    /// Sorts volumes into buildings. Models are stacked volumes, one closed box (or roof shape) per storey or part,
    /// as Numbat's nbGFA expects them. Volumes that touch (stacked, or side by side) belong to the same building, and so do
    /// volumes in the same Rhino group, which lets users join parts that don't touch.
    /// </summary>
    internal static class AfBuildings
    {
        /// <returns>Lists of volume indices, one list per building.</returns>
        public static List<List<int>> Group(IList<Brep> volumes, IList<int[]> groups, double tol)
        {
            int n = volumes.Count;
            var parent = Enumerable.Range(0, n).ToArray();
            int Root(int i) => parent[i] == i ? i : parent[i] = Root(parent[i]);
            void Join(int a, int b) => parent[Root(a)] = Root(b);

            var boxes = volumes.Select(v => { var b = v.GetBoundingBox(true); b.Inflate(tol * 2); return b; }).ToList();
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (Root(i) == Root(j)) continue;
                    if (!Overlap(boxes[i], boxes[j])) continue;
                    if (Touch(volumes[i], volumes[j], tol)) Join(i, j);
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

            return Enumerable.Range(0, n).GroupBy(Root).Select(g => g.ToList()).ToList();
        }

        /// <summary>
        /// True if two volumes touch or overlap: a corner of one lies on the other (stacked storeys, a smaller
        /// storey on a bigger one or the other way round), or failing that, their surfaces intersect.
        /// </summary>
        private static bool Touch(Brep a, Brep b, double tol)
        {
            if (CornerOn(a, b, tol) || CornerOn(b, a, tol)) return true;
            return Intersection.BrepBrep(a, b, tol, out Curve[] curves, out Point3d[] points) &&
                   ((curves?.Length ?? 0) > 0 || (points?.Length ?? 0) > 0);
        }

        private static bool CornerOn(Brep a, Brep b, double tol)
        {
            foreach (BrepVertex v in a.Vertices)
            {
                if (b.ClosestPoint(v.Location).DistanceTo(v.Location) <= tol * 2) return true;
            }
            return false;
        }

        private static bool Overlap(BoundingBox a, BoundingBox b) =>
            a.Min.X <= b.Max.X && b.Min.X <= a.Max.X &&
            a.Min.Y <= b.Max.Y && b.Min.Y <= a.Max.Y &&
            a.Min.Z <= b.Max.Z && b.Min.Z <= a.Max.Z;
    }
}
