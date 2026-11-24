using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Geometry;

namespace Numbat.Commands.Analysis.NumbatAf
{
    /// <summary>One flat, vertical outer wall of a building and its Abstandsfläche.</summary>
    internal sealed class Wall
    {
        public int Building;
        public Point3d Start, End;      // base line at ground level
        public Vector3d Outward;        // unit, horizontal
        public double WallHeight;       // ground to where the wall meets the roof (model units)
        public double RoofHeight;       // roof height above that point
        public double RoofPitchDeg;     // 0 for flat roofs
        public double RoofShare;        // share of the roof height counted in H (1/3 or 1)
        public bool Gable;              // sloped top edge: H runs from HStart to HEnd along the wall
        public double HStart, HEnd;
        public double H;                // the larger of HStart and HEnd
        public double DepthStart, DepthEnd;
        public double Depth;            // the larger of DepthStart and DepthEnd
        public Polyline Area;           // closed outline on the ground (a rectangle unless under a gable)

        public Vector3d Direction => End - Start;

        /// <summary>Angle between two walls in plan, 0..90 degrees.</summary>
        public static double AngleBetween(Wall a, Wall b)
        {
            Vector3d da = a.Direction, db = b.Direction;
            da.Unitize(); db.Unitize();
            double cos = Math.Abs(da * db);
            return RhinoMath.ToDegrees(Math.Acos(Math.Min(1.0, cos)));
        }
    }

    /// <summary>Finds the outer walls of a building and works out H for each (HBauO § 6 Abs. 4).</summary>
    internal static class AfWalls
    {
        private static readonly double VerticalTol = Math.Sin(RhinoMath.ToRadians(2.0));

        // Walls of one façade drawn by hand are rarely exactly parallel: within 1° they count as facing the same way.
        private static readonly double SameDirection = Math.Cos(RhinoMath.ToRadians(1.0));

        // Faces tilted between 2° and 15° from vertical: meant as walls, but too far off to measure. Steeper roofs are rare.
        private static readonly double LeaningLimit = Math.Sin(RhinoMath.ToRadians(15.0));

        /// <summary>
        /// Finds the outer walls of one building made of stacked volumes (one closed Brep per storey or part, the
        /// way nbGFA expects them). No boolean union: the wall faces of all volumes that lie in one plane form a façade,
        /// and the façade is "folded down" to the ground. Along the façade, each stretch takes the highest H of the
        /// volumes above it, so a wall under a taller storey counts with the full height, and an upper storey that
        /// covers only part of the floor below splits the façade. Faces between two touching volumes of the building
        /// (back to back, facing each other) are inside the building and are left out.
        /// </summary>
        /// <param name="skipped">Vertical faces that are not flat (curved walls), not handled yet.</param>
        /// <param name="leaning">Faces that lean 2–15° off vertical: probably walls, but left out.</param>
        public static List<Wall> Find(IList<Brep> volumes, int building, AfRules rules, double metre, double tol, out int skipped, out int leaning)
        {
            skipped = 0;
            leaning = 0;
            double groundZ = volumes.Min(v => v.GetBoundingBox(true).Min.Z);
            // Walls count as flat within 5 mm (or 10 × the model tolerance, if larger): merging two almost-collinear
            // faces of a real model leaves a face that is a hair off flat, and 5 mm is nothing against depths of 2.50 m or more.
            double planeTol = Math.Max(tol * 10, 0.005 * metre);
            var facades = new List<Facade>();

            for (int v = 0; v < volumes.Count; v++)
            {
                Brep brep = volumes[v];
                double flip = brep.SolidOrientation == BrepSolidOrientation.Inward ? -1 : 1;

                foreach (BrepFace face in brep.Faces)
                {
                    Vector3d n = Normal(face, flip);
                    if (Math.Abs(n.Z) > VerticalTol)
                    {
                        if (Math.Abs(n.Z) < LeaningLimit) leaning++;
                        continue; // roof, floor or sloped face
                    }

                    if (!face.TryGetPlane(out _, planeTol))
                    {
                        skipped++;
                        continue;
                    }

                    var outward = new Vector3d(n.X, n.Y, 0);
                    outward.Unitize();
                    List<Point3d> pts = LoopPoints(face);
                    if (pts.Count < 2) continue;

                    Facade f = facades.FirstOrDefault(x => x.Outward * outward > SameDirection &&
                                                           Math.Abs((pts[0] - x.Origin) * x.Outward) < planeTol);
                    if (f == null)
                    {
                        f = new Facade(outward, new Point3d(pts[0].X, pts[0].Y, groundZ));
                        facades.Add(f);
                    }

                    double umin = pts.Min(f.U), umax = pts.Max(f.U);
                    if (umax - umin < tol) continue;
                    double faceMinZ = pts.Min(p => p.Z), faceMaxZ = pts.Max(p => p.Z);
                    f.Faces.Add(new FacePart { Volume = v, A = f.At(umin), B = f.At(umax), Z0 = faceMinZ, Z1 = faceMaxZ });

                    List<Top> tops = TopEdges(brep, face, flip, faceMinZ, groundZ, rules, tol, f.U);
                    if (tops.Count == 0)
                    {
                        tops.Add(new Top { U0 = umin, U1 = umax, ZLo = faceMaxZ, WallHeight = faceMaxZ - groundZ, RoofShare = 1, H = faceMaxZ - groundZ });
                    }
                    foreach (Top t in tops) t.Volume = v;
                    f.Tops.AddRange(tops);
                }
            }

            // A pitched roof modelled as its own volume on top of a storey: the storey's wall below the eaves gets
            // the roof added, just as if the roof were part of the same volume.
            foreach (Eave e in Eaves(volumes, tol))
            {
                foreach (Facade f in facades)
                {
                    if (Math.Abs((e.A - f.Origin) * f.Outward) > planeTol || Math.Abs((e.B - f.Origin) * f.Outward) > planeTol) continue;
                    if (e.Uphill * f.Outward > 0) continue; // the roof must rise away from the façade, into the building
                    double share = rules.RoofShare(e.PitchDeg);
                    f.Tops.Add(new Top
                    {
                        Volume = e.Volume,
                        U0 = Math.Min(f.U(e.A), f.U(e.B)),
                        U1 = Math.Max(f.U(e.A), f.U(e.B)),
                        ZLo = e.Z,
                        WallHeight = e.Z - groundZ,
                        RoofHeight = e.RoofHeight,
                        RoofPitchDeg = e.PitchDeg,
                        RoofShare = share,
                        H = e.Z - groundZ + e.RoofHeight * share,
                    });
                }
            }

            var walls = new List<Wall>();
            foreach (Facade f in facades)
            {
                // The facing façade in the same plane, if any: where it belongs to another volume, the wall is inside.
                Facade back = facades.FirstOrDefault(x => x.Outward * f.Outward < -SameDirection &&
                                                          Math.Abs((x.Origin - f.Origin) * f.Outward) < planeTol);
                List<Top> tops = back == null ? f.Tops : f.Tops.SelectMany(t => Outside(t, f, back, tol)).ToList();
                if (tops.Count == 0) continue;

                foreach (var (u0, u1, top) in Segments(tops, tol))
                {
                    var wall = new Wall
                    {
                        Building = building,
                        Start = f.At(u0),
                        End = f.At(u1),
                        Outward = f.Outward,
                        WallHeight = top.WallHeight,
                        RoofHeight = top.RoofHeight,
                        RoofPitchDeg = top.RoofPitchDeg,
                        RoofShare = top.RoofShare,
                        Gable = top.Gable,
                        HStart = top.At(u0),
                        HEnd = top.At(u1),
                    };
                    wall.H = Math.Max(wall.HStart, wall.HEnd);
                    wall.DepthStart = rules.Depth(wall.HStart, metre);
                    wall.DepthEnd = rules.Depth(wall.HEnd, metre);
                    wall.Depth = Math.Max(wall.DepthStart, wall.DepthEnd);
                    wall.Area = AreaOutline(wall, rules, metre);
                    walls.Add(wall);
                }
            }
            return walls;
        }

        /// <summary>
        /// The Abstandsfläche on the ground. A rectangle where H is constant; under a gable the depth follows the
        /// wall's outline, 0.4 H at every point but never less than the minimum (so it may have one extra corner
        /// where the minimum takes over).
        /// </summary>
        private static Polyline AreaOutline(Wall w, AfRules rules, double metre)
        {
            Vector3d n = w.Outward;
            var pts = new List<Point3d> { w.Start, w.End, w.End + n * w.DepthEnd };
            double min = rules.MinDepthM * metre;
            double f0 = rules.Factor * w.HStart, f1 = rules.Factor * w.HEnd;
            if ((f0 - min) * (f1 - min) < 0)
            {
                double t = (min - f0) / (f1 - f0);
                pts.Add(w.Start + (w.End - w.Start) * t + n * min);
            }
            pts.Add(w.Start + n * w.DepthStart);
            pts.Add(w.Start);
            return new Polyline(pts);
        }

        /// <summary>The lower, horizontal edge of a sloped roof face.</summary>
        private sealed class Eave
        {
            public int Volume;
            public Point3d A, B;
            public double Z, PitchDeg, RoofHeight;
            public Vector3d Uphill; // horizontal direction the roof rises in
        }

        private static IEnumerable<Eave> Eaves(IList<Brep> volumes, double tol)
        {
            for (int v = 0; v < volumes.Count; v++)
            {
                Brep brep = volumes[v];
                double flip = brep.SolidOrientation == BrepSolidOrientation.Inward ? -1 : 1;
                foreach (BrepFace face in brep.Faces)
                {
                    Vector3d n = Normal(face, flip);
                    if (n.Z <= VerticalTol) continue; // walls and undersides
                    double pitch = RhinoMath.ToDegrees(Math.Acos(Math.Min(1.0, n.Z)));
                    if (pitch < 0.5) continue; // flat roof

                    List<Point3d> pts = LoopPoints(face);
                    double zMin = pts.Min(p => p.Z), zMax = pts.Max(p => p.Z);
                    var uphill = new Vector3d(-n.X, -n.Y, 0);
                    uphill.Unitize();
                    foreach (BrepTrim trim in face.OuterLoop.Trims)
                    {
                        if (trim.Edge == null) continue;
                        Point3d a = trim.Edge.PointAtStart, b = trim.Edge.PointAtEnd;
                        if (Math.Abs(a.Z - zMin) > tol || Math.Abs(b.Z - zMin) > tol || a.DistanceTo(b) < tol) continue;
                        yield return new Eave { Volume = v, A = a, B = b, Z = zMin, PitchDeg = pitch, RoofHeight = zMax - zMin, Uphill = uphill };
                    }
                }
            }
        }

        /// <summary>The parts of a top edge that are not shared with a touching volume's back-to-back wall.</summary>
        private static IEnumerable<Top> Outside(Top t, Facade f, Facade back, double tol)
        {
            var keep = new List<(double a, double b)> { (t.U0, t.U1) };
            foreach (FacePart p in back.Faces)
            {
                if (p.Volume == t.Volume) continue;
                if (!(p.Z0 < t.ZLo - tol && p.Z1 >= t.ZLo - tol)) continue; // not behind the top of this wall
                double a = Math.Min(f.U(p.A), f.U(p.B)), b = Math.Max(f.U(p.A), f.U(p.B));
                keep = keep.SelectMany(k => Subtract(k, a, b, tol)).ToList();
            }
            foreach (var (a, b) in keep) yield return t.With(a, b);
        }

        private static IEnumerable<(double, double)> Subtract((double a, double b) k, double a, double b, double tol)
        {
            if (b <= k.a + tol || a >= k.b - tol)
            {
                yield return k;
                yield break;
            }
            if (a > k.a + tol) yield return (k.a, a);
            if (b < k.b - tol) yield return (b, k.b);
        }

        /// <summary>All wall faces of a building that lie in one vertical plane, facing the same way.</summary>
        private sealed class Facade
        {
            public readonly Vector3d Outward, Dir;
            public readonly Point3d Origin; // on the plane, at ground level
            public readonly List<Top> Tops = new List<Top>();
            public readonly List<FacePart> Faces = new List<FacePart>();

            public Facade(Vector3d outward, Point3d origin)
            {
                Outward = outward;
                Origin = origin;
                Dir = Vector3d.CrossProduct(Vector3d.ZAxis, outward);
            }

            public double U(Point3d p) => (p - Origin) * Dir;
            public Point3d At(double u) => Origin + Dir * u;
        }

        /// <summary>The extent of one wall face: base points at ground level and its height range.</summary>
        private sealed class FacePart
        {
            public int Volume;
            public Point3d A, B;
            public double Z0, Z1;
        }

        /// <summary>A top edge of a wall face, its stretch along the façade and the H it gives.</summary>
        private sealed class Top
        {
            public int Volume;
            public bool Gable;
            public double U0, U1, ZLo, WallHeight, RoofHeight, RoofPitchDeg, RoofShare;
            public double H0, H1; // H at U0 and U1 (equal unless under a gable)

            /// <summary>Sets a constant H along the edge.</summary>
            public double H { set { H0 = H1 = value; } }

            /// <summary>H at a point along the façade (linear between the ends).</summary>
            public double At(double u) => U1 - U0 < 1e-12 ? H0 : H0 + (H1 - H0) * (u - U0) / (U1 - U0);

            public Top With(double u0, double u1)
            {
                var t = (Top)MemberwiseClone();
                t.H0 = At(u0);
                t.H1 = At(u1);
                t.U0 = u0;
                t.U1 = u1;
                return t;
            }
        }

        /// <summary>
        /// Top edges are where the wall meets a roof (or flat roof) face. Eaves edge (horizontal): the wall height
        /// is the edge height, and the roof adds one third of how far that roof face rises above it (all of it at
        /// 70° or steeper). Gable (sloped top edge): the wall itself reaches up to the ridge, so H follows the edge,
        /// from its height at one end to its height at the other, with no roof share (Hamburg FAQ zu § 6,
        /// "Giebeldreieck": the wall height runs to the top of the wall, i.e. the ridge).
        /// </summary>
        private static List<Top> TopEdges(Brep brep, BrepFace face, double flip, double faceMinZ, double groundZ,
                                          AfRules rules, double tol, Func<Point3d, double> u)
        {
            var tops = new List<Top>();
            foreach (BrepTrim trim in face.OuterLoop.Trims)
            {
                BrepEdge edge = trim.Edge;
                if (edge == null) continue;
                Point3d a = edge.PointAtStart, b = edge.PointAtEnd;
                double zLo = Math.Min(a.Z, b.Z), zHi = Math.Max(a.Z, b.Z);
                if (zHi <= faceMinZ + tol) continue; // bottom edge
                double u0 = Math.Min(u(a), u(b)), u1 = Math.Max(u(a), u(b));
                if (u1 - u0 < tol) continue; // vertical edge

                foreach (int other in edge.AdjacentFaces())
                {
                    if (other == face.FaceIndex) continue;
                    BrepFace roof = brep.Faces[other];
                    Vector3d rn = Normal(roof, flip);
                    if (Math.Abs(rn.Z) <= VerticalTol || rn.Z < 0) continue; // another wall, or an underside

                    if (zHi - zLo >= tol)
                    {
                        bool aFirst = u(a) <= u(b);
                        tops.Add(new Top
                        {
                            Gable = true,
                            U0 = u0, U1 = u1,
                            ZLo = zLo,
                            WallHeight = zHi - groundZ,
                            RoofShare = 1,
                            H0 = (aFirst ? a.Z : b.Z) - groundZ,
                            H1 = (aFirst ? b.Z : a.Z) - groundZ,
                        });
                        continue;
                    }

                    double pitch = RhinoMath.ToDegrees(Math.Acos(Math.Min(1.0, rn.Z)));
                    double roofH = pitch < 0.5 ? 0 : LoopPoints(roof).Max(p => p.Z) - zLo;
                    if (roofH < tol) pitch = 0;
                    double share = rules.RoofShare(pitch);
                    tops.Add(new Top
                    {
                        U0 = u0, U1 = u1,
                        ZLo = zLo,
                        WallHeight = zLo - groundZ,
                        RoofHeight = roofH,
                        RoofPitchDeg = pitch,
                        RoofShare = share,
                        H = zLo - groundZ + roofH * share,
                    });
                }
            }
            return tops;
        }

        /// <summary>
        /// Splits the wall where its height changes (stepped façades): each stretch takes the highest H of the top
        /// edges above it, and neighbouring stretches on the same height line are joined again.
        /// Stretches with no top edge (gaps between separate parts of the façade) get no wall.
        /// </summary>
        private static IEnumerable<(double u0, double u1, Top top)> Segments(List<Top> tops, double tol)
        {
            var cuts = tops.SelectMany(t => new[] { t.U0, t.U1 }).OrderBy(c => c).ToList();
            var pieces = new List<(double u0, double u1, Top top)>();
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                double a = cuts[i], b = cuts[i + 1];
                if (b - a < tol) continue;
                double mid = (a + b) / 2;
                Top best = tops.Where(t => t.U0 - tol <= mid && mid <= t.U1 + tol).OrderByDescending(t => t.At(mid)).FirstOrDefault();
                if (best == null) continue;
                // Join with the previous stretch if it continues the same height line (flat and equal, or the same slope).
                Top prev = pieces.Count > 0 ? pieces[pieces.Count - 1].top : null;
                if (prev != null && pieces[pieces.Count - 1].u1 >= a - tol &&
                    Math.Abs(prev.At(a) - best.At(a)) < tol && Math.Abs(prev.At(b) - best.At(b)) < tol)
                {
                    var last = pieces[pieces.Count - 1];
                    pieces[pieces.Count - 1] = (last.u0, b, last.top);
                }
                else
                {
                    pieces.Add((a, b, best));
                }
            }
            return pieces;
        }

        private static Vector3d Normal(BrepFace face, double flip)
        {
            double u = face.Domain(0).Mid, v = face.Domain(1).Mid;
            Vector3d n = face.TryGetPlane(out Plane pl, RhinoMath.ZeroTolerance * 1e6) ? pl.Normal : face.NormalAt(u, v);
            if (face.OrientationIsReversed) n = -n;
            n *= flip;
            n.Unitize();
            return n;
        }

        private static List<Point3d> LoopPoints(BrepFace face)
        {
            var pts = new List<Point3d>();
            foreach (BrepTrim trim in face.OuterLoop.Trims)
            {
                if (trim.Edge == null) continue;
                pts.Add(trim.Edge.PointAtStart);
                pts.Add(trim.Edge.PointAtEnd);
                pts.Add(trim.Edge.PointAt(trim.Edge.Domain.Mid));
            }
            return pts;
        }
    }
}
