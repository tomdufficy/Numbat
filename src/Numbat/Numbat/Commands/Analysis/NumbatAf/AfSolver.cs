using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace Numbat.Commands.Analysis.NumbatAf
{
    internal enum FlagKind { Overlap, InBuilding, InOtherPlot, PastRoadCentre, NearRoadCentre }

    /// <summary>A rule break found by the checks: the conflict region and where to put its marker.</summary>
    internal sealed class Flag
    {
        public int Number;          // 1, 2, ... shown on the marker and in the window's list
        public FlagKind Kind;
        public Curve Region;
        public Point3d DotAt;
        public double Area;         // m²
        public string Text;         // e.g. "Overlap B3/B4"
        public string Note;         // e.g. "0.1 mm gap: one building?"
        public string Why;          // which rule, in plain words, with the numbers: shown on hover and in reports
        public bool IsCheck;        // not a clash, something to look at (e.g. close to a road's centre)
        public int[] Buildings;     // the buildings involved

        public string Describe() => IsCheck
            ? $"{Text} (check){(Note == null ? "" : $" ({Note})")}"
            : $"{Text}, {Area.ToString("0.0", CultureInfo.InvariantCulture)} m²{(Note == null ? "" : $" ({Note})")}";
    }

    /// <summary>
    /// The Abstandsfläche of one face (side) of a building: every wall of the building facing the same way,
    /// stepped ones included, merged into closed outlines and clipped to what lies outside the building's own
    /// footprint (Hamburg FAQ zu § 6 Abs. 2: gestaffelte Außenwände ergeben als Ganzes eine Abstandsfläche).
    /// Usually one region; two when the face has parts that don't meet (e.g. the two wings of a U).
    /// </summary>
    internal sealed class AfFace
    {
        public int Building;
        public Vector3d Outward;
        public readonly List<Wall> Walls = new List<Wall>();
        public readonly List<Curve> Regions = new List<Curve>();
        public double DepthMin, DepthMax;

        /// <summary>Compass direction the face looks to (north = +Y): "north", "south-east", ...</summary>
        public string Facing
        {
            get
            {
                string[] names = { "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east" };
                double deg = RhinoMath.ToDegrees(Math.Atan2(Outward.Y, Outward.X));
                int i = (int)Math.Round(((deg % 360) + 360) % 360 / 45.0) % 8;
                return names[i];
            }
        }
    }

    /// <summary>
    /// A depth dimension, as on a plan: a line from the middle of the wall (<see cref="Start"/>) straight out to the
    /// edge of its Abstandsfläche (<see cref="End"/>), and the depth written beside it on <see cref="Plane"/>
    /// (centred, reading along the line), <see cref="Height"/> tall in model units. Dimensions that would collide with
    /// a deeper one are left out.
    /// </summary>
    internal sealed class AfLabel
    {
        public Wall Wall;
        public Point3d Start, End;
        public Plane Plane;
        public double Height;
        public string Text;
        public Point3d At => Plane.Origin;
    }

    /// <summary>One building as the solver gets it: its volumes, role and plot.</summary>
    internal sealed class BuildingInput
    {
        public string Name;
        public List<Brep> Volumes = new List<Brep>();
        public Curve Plot;
    }

    internal sealed class AfResult
    {
        public readonly List<BuildingInput> Buildings = new List<BuildingInput>();
        public readonly List<Wall> Walls = new List<Wall>();
        public readonly List<AfFace> Faces = new List<AfFace>();
        public readonly List<AfLabel> Labels = new List<AfLabel>();
        public readonly List<List<Curve>> Footprints = new List<List<Curve>>();  // plan outline, per building
        public readonly List<Flag> Flags = new List<Flag>();
        public int SkippedFaces;
        public int LeaningWalls;
        public int[] BuildingColours = new int[0];   // colour index per building (set by AfModel)
        public readonly List<Curve> Roads = new List<Curve>();   // road centre lines the checks used

        /// <summary>Real clashes, not things to check.</summary>
        public int ClashCount => Flags.Count(f => !f.IsCheck);

        public int FlagCount(int building) => Flags.Count(f => !f.IsCheck && f.Buildings.Contains(building));
    }

    /// <summary>
    /// Builds the Abstandsflächen of every building and checks them. All buildings get areas; flags are only raised
    /// between every pair of buildings: no building is treated differently.
    /// </summary>
    internal static class AfSolver
    {
        private static readonly double SameDirection = Math.Cos(RhinoMath.ToRadians(1.0));

        /// <param name="roads">Road centre lines (may be empty).</param>
        public static AfResult Run(IList<BuildingInput> buildings, AfRules rules, RhinoDoc doc, IList<Curve> roads = null)
        {
            double tol = doc.ModelAbsoluteTolerance;
            double metre = RhinoMath.UnitScale(UnitSystem.Meters, doc.ModelUnitSystem);
            double minArea = 0.1 * metre * metre; // ignore slivers under 0.1 m²
            var r = new AfResult();

            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingInput b = buildings[i];
                var tidy = new BuildingInput
                {
                    Name = b.Name,
                    Plot = b.Plot,
                    Volumes = b.Volumes.Select(v => Tidy(v, tol, doc.ModelAngleToleranceRadians)).ToList(),
                };
                r.Buildings.Add(tidy);
                List<Wall> walls = AfWalls.Find(tidy.Volumes, i, rules, metre, tol, out int skipped, out int leaning);
                r.Walls.AddRange(walls);
                r.SkippedFaces += skipped;
                r.LeaningWalls += leaning;

                List<Curve> footprint = Footprint(tidy.Volumes, tol);
                r.Footprints.Add(footprint);
                foreach (AfFace face in Faces(walls, i, footprint, tol, minArea)) r.Faces.Add(face);
            }
            Labels(r, metre, tol);

            CheckOverlaps(r, rules, tol, minArea, metre);
            CheckBuildings(r, tol, minArea, metre);
            CheckPlots(r, tol, minArea, metre);
            if (roads != null) r.Roads.AddRange(roads);
            CheckRoads(r, roads, tol, minArea, metre);
            // Clashes first, then things to check; numbered in that order.
            List<Flag> ordered = r.Flags.Where(f => !f.IsCheck).Concat(r.Flags.Where(f => f.IsCheck)).ToList();
            r.Flags.Clear();
            r.Flags.AddRange(ordered);
            for (int k = 0; k < r.Flags.Count; k++) r.Flags[k].Number = k + 1;
            return r;
        }

        /// <summary>One face per flat side, so each wall face is found once (extrusions can have kinked faces).</summary>
        private static Brep Tidy(Brep b, double tol, double angleTol)
        {
            Brep c = b.DuplicateBrep();
            c.Faces.SplitKinkyFaces();
            c.MergeCoplanarFaces(tol, angleTol);
            return c;
        }

        // ---- faces ---------------------------------------------------------------------

        /// <summary>
        /// Groups a building's walls by the way they face, merges each group's areas into closed outlines and keeps
        /// what lies outside the building's own footprint. A stepped (set-back) wall is measured from its own
        /// position folded down to the ground, so part of its area can lie over the building's lower storeys; that
        /// part is inside the building and is dropped.
        /// </summary>
        private static IEnumerable<AfFace> Faces(List<Wall> walls, int building, List<Curve> footprint, double tol, double minArea)
        {
            var faces = new List<AfFace>();
            foreach (Wall w in walls)
            {
                AfFace f = faces.FirstOrDefault(x => x.Outward * w.Outward > SameDirection);
                if (f == null)
                {
                    f = new AfFace { Building = building, Outward = w.Outward };
                    faces.Add(f);
                }
                f.Walls.Add(w);
            }

            foreach (AfFace f in faces)
            {
                List<Curve> areas = f.Walls.Select(w => (Curve)w.Area.ToPolylineCurve()).ToList();
                Curve[] union = areas.Count > 1 ? Curve.CreateBooleanUnion(areas, tol) : null;
                List<Curve> merged = union != null && union.Length > 0 ? union.ToList() : areas;

                foreach (Curve c in merged)
                {
                    foreach (Curve piece in Difference(new List<Curve> { c }, footprint, tol))
                    {
                        AreaMassProperties amp = AreaMassProperties.Compute(piece);
                        if (amp == null || amp.Area < minArea) continue;
                        f.Regions.Add(Clean(piece, tol));
                    }
                }
                f.DepthMin = f.Walls.Min(w => Math.Min(w.DepthStart, w.DepthEnd));
                f.DepthMax = f.Walls.Max(w => w.Depth);
                if (f.Regions.Count > 0) yield return f;
            }
        }

        /// <summary>A closed polyline with collinear points merged where possible, so each face is one tidy outline.</summary>
        private static Curve Clean(Curve c, double tol)
        {
            if (c.TryGetPolyline(out Polyline pl))
            {
                pl.MergeColinearSegments(RhinoMath.ToRadians(0.1), true);
                if (pl.Count >= 4 && pl.IsClosed) return pl.ToPolylineCurve();
            }
            return c.Simplify(CurveSimplifyOptions.All, tol, RhinoMath.ToRadians(0.1)) ?? c;
        }

        // ---- labels --------------------------------------------------------------------

        private const double CharWidth = 0.62;   // average character width as a share of the text height
        private const double TextM = 0.35;       // dimension text height in metres
        private const double MinTextM = 0.12;    // smaller than this and the dimension is left out

        /// <summary>
        /// One depth dimension per wall whose area reaches outside the building: from the middle of the wall straight
        /// out to the edge of its area, with the text beside the part outside the building. Deeper walls are placed
        /// first; a dimension that would overlap one already placed is dropped, as is one for a wall whose area lies
        /// inside a higher wall's area of the same face.
        /// </summary>
        private static void Labels(AfResult r, double metre, double tol)
        {
            var placed = new List<BoundingBox>();
            foreach (Wall w in r.Walls.OrderByDescending(w => w.Depth))
            {
                if (Hidden(w, r.Walls, tol)) continue;
                (Point3d mid, double length)? spot = OutsideSpot(w, r.Footprints[w.Building], tol);
                if (spot == null) continue;

                Point3d start = (w.Start + w.End) / 2;
                Point3d end = FarPoint(w, start, tol);
                string text = $"{(start.DistanceTo(end) / metre).ToString("0.00", CultureInfo.InvariantCulture)} m";
                double h = Math.Min(TextM * metre, 0.9 * spot.Value.length / (text.Length * CharWidth));
                if (h < MinTextM * metre) continue;

                // Read along the dimension line, from the bottom or the right of the plan (ISO 129).
                Vector3d x = w.Outward;
                double angle = RhinoMath.ToDegrees(Math.Atan2(x.Y, x.X));
                if (angle <= -60 || angle > 120) x = -x;
                Vector3d y = Vector3d.CrossProduct(Vector3d.ZAxis, x);
                var plane = new Plane(spot.Value.mid + y * (h * 0.8), x, y);

                double halfW = text.Length * CharWidth * h / 2, halfH = h / 2;
                var box = new BoundingBox(new[]
                {
                    plane.PointAt(-halfW, -halfH), plane.PointAt(halfW, -halfH), plane.PointAt(halfW, halfH), plane.PointAt(-halfW, halfH),
                    spot.Value.mid,
                });
                if (placed.Any(bx => Overlaps(bx, box))) continue;
                placed.Add(box);
                r.Labels.Add(new AfLabel { Wall = w, Start = start, End = end, Plane = plane, Height = h, Text = text });
            }
        }

        /// <summary>Where the line out from the middle of the wall leaves the wall's own area (its depth there).</summary>
        private static Point3d FarPoint(Wall w, Point3d start, double tol)
        {
            var ray = new LineCurve(start, start + w.Outward * (w.Depth * 2 + tol * 100));
            CurveIntersections xs = Intersection.CurveCurve(ray, w.Area.ToPolylineCurve(), tol, tol);
            Point3d best = start + w.Outward * ((w.DepthStart + w.DepthEnd) / 2);
            double far = 0;
            if (xs != null)
            {
                foreach (IntersectionEvent x in xs)
                {
                    double d = x.PointA.DistanceTo(start);
                    if (d > far) { far = d; best = x.PointA; }
                }
            }
            return best;
        }

        /// <summary>
        /// True when a wall's area is entirely inside the area of another wall of the same building facing the same
        /// way (a set-back storey behind a higher wall below): it doesn't shape the outline, so it gets no label.
        /// Tested at the far edge of its area, a hair inside.
        /// </summary>
        private static bool Hidden(Wall w, List<Wall> all, double tol)
        {
            Point3d from = (w.Start + w.End) / 2;
            Point3d far = from + w.Outward * ((w.DepthStart + w.DepthEnd) / 2 - tol * 20);
            foreach (Wall o in all)
            {
                if (o == w || o.Building != w.Building || o.Outward * w.Outward < SameDirection) continue;
                if (o.Area.ToPolylineCurve().Contains(far, Plane.WorldXY, tol) == PointContainment.Inside) return true;
            }
            return false;
        }

        /// <summary>
        /// The part of a wall's area outside the building, on the line out from the middle of the wall: its middle
        /// point and length. Null when all of it lies over the building itself.
        /// </summary>
        private static (Point3d mid, double length)? OutsideSpot(Wall w, List<Curve> footprint, double tol)
        {
            Point3d from = (w.Start + w.End) / 2;
            double depth = (w.DepthStart + w.DepthEnd) / 2;
            var ray = new LineCurve(from, from + w.Outward * depth);
            var cuts = new List<double> { ray.Domain.Min, ray.Domain.Max };
            List<Curve> flat = footprint.Select(fp => Flatten(fp, from.Z)).ToList();
            foreach (Curve fp in flat)
            {
                CurveIntersections xs = Intersection.CurveCurve(ray, fp, tol, tol);
                if (xs == null) continue;
                foreach (IntersectionEvent x in xs) cuts.Add(x.ParameterA);
            }
            cuts.Sort();
            for (int i = cuts.Count - 1; i > 0; i--)
            {
                Point3d a = ray.PointAt(cuts[i - 1]), b = ray.PointAt(cuts[i]);
                if (a.DistanceTo(b) < tol * 10) continue;
                Point3d mid = (a + b) / 2;
                if (!flat.Any(fp => fp.Contains(mid, Plane.WorldXY, tol) == PointContainment.Inside)) return (mid, a.DistanceTo(b));
            }
            return null;
        }

        // ---- checks --------------------------------------------------------------------

        /// <summary>Angle between two faces in plan, 0..90 degrees (0 = parallel, facing or back to back).</summary>
        private static double AngleBetween(AfFace a, AfFace b) =>
            RhinoMath.ToDegrees(Math.Acos(Math.Min(1.0, Math.Abs(a.Outward * b.Outward))));

        /// <summary>
        /// HBauO § 6 Abs. 3: Abstandsflächen must not overlap unless the walls are at more than 75° to each other.
        /// Stepped walls of one building facing the same way are one face, so they never overlap each other.
        /// Every pair of faces is checked; where three or more overlap, each pair is flagged.
        /// </summary>
        private static void CheckOverlaps(AfResult r, AfRules rules, double tol, double minArea, double metre)
        {
            for (int i = 0; i < r.Faces.Count; i++)
            {
                for (int j = i + 1; j < r.Faces.Count; j++)
                {
                    AfFace a = r.Faces[i], b = r.Faces[j];
                    if (AngleBetween(a, b) > rules.OverlapExemptDeg) continue;
                    if (a.Building == b.Building && a.Outward * b.Outward > 0) continue;
                    string who = a.Building == b.Building ? $"within {r.Buildings[a.Building].Name}"
                                                          : $"{r.Buildings[a.Building].Name}/{r.Buildings[b.Building].Name}";
                    foreach (Curve ra in a.Regions)
                    {
                        foreach (Curve rb in b.Regions)
                        {
                            if (!Overlaps(ra.GetBoundingBox(true), rb.GetBoundingBox(true))) continue;
                            foreach (Curve c in Curve.CreateBooleanIntersection(ra, rb, tol) ?? Array.Empty<Curve>())
                            {
                                AddFlag(r, FlagKind.Overlap, c, minArea, metre, $"Overlap {who}", OverlapWhy(r, a, b, c, rules, metre), a.Building, b.Building);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>HBauO § 6 Abs. 1: the Abstandsfläche must be kept free of buildings.</summary>
        private static void CheckBuildings(AfResult r, double tol, double minArea, double metre)
        {
            foreach (AfFace f in r.Faces)
            {
                foreach (Curve area in f.Regions)
                {
                    BoundingBox box = area.GetBoundingBox(true);
                    double z = area.PointAtStart.Z;
                    for (int b = 0; b < r.Buildings.Count; b++)
                    {
                        if (b == f.Building) continue;
                        foreach (Curve fp in r.Footprints[b])
                        {
                            if (!Overlaps(box, fp.GetBoundingBox(true))) continue;
                            foreach (Curve c in Curve.CreateBooleanIntersection(area, Flatten(fp, z), tol) ?? Array.Empty<Curve>())
                            {
                                AddFlag(r, FlagKind.InBuilding, c, minArea, metre,
                                    $"{r.Buildings[f.Building].Name} into {r.Buildings[b].Name}",
                                    $"§ 6 Abs. 1: the area in front of a wall must be kept free of buildings, but {r.Buildings[f.Building].Name}'s " +
                                    $"Abstandsfläche (up to {M(f.DepthMax, metre)} m deep) reaches into {r.Buildings[b].Name}.", f.Building, b);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// HBauO § 6 Abs. 2: Abstandsflächen must lie on the building's own plot. Only the part that reaches another
        /// building's plot is flagged, for every building. Leaving the own plot towards a road or open land is not
        /// flagged (the half-road rule is not checked yet), and buildings without a plot are only checked against
        /// the plots that exist.
        /// </summary>
        private static void CheckPlots(AfResult r, double tol, double minArea, double metre)
        {
            if (!r.Buildings.Any(b => b.Plot != null)) return;
            foreach (AfFace f in r.Faces)
            {
                BuildingInput own = r.Buildings[f.Building];
                foreach (Curve area in f.Regions)
                {
                    double z = area.PointAtStart.Z;
                    List<Curve> outside = own.Plot == null
                        ? new List<Curve> { area }
                        : Difference(new List<Curve> { area }, new List<Curve> { Flatten(own.Plot, z) }, tol);
                    for (int b = 0; b < r.Buildings.Count; b++)
                    {
                        if (b == f.Building || r.Buildings[b].Plot == null) continue;
                        Curve plot = Flatten(r.Buildings[b].Plot, z);
                        foreach (Curve piece in outside)
                        {
                            foreach (Curve c in Curve.CreateBooleanIntersection(piece, plot, tol) ?? Array.Empty<Curve>())
                            {
                                AddFlag(r, FlagKind.InOtherPlot, c, minArea, metre, $"{own.Name} on plot of {r.Buildings[b].Name}",
                                        $"§ 6 Abs. 2: Abstandsflächen must lie on the building's own plot, but part of {own.Name}'s lies on " +
                                        $"{r.Buildings[b].Name}'s plot.", f.Building, b);
                            }
                        }
                        outside = Difference(outside, new List<Curve> { plot }, tol);
                    }
                    // What leaves the own plot without reaching another plot (a road, open land, a plot nobody
                    // drew) is not flagged. In principle it may cover a public road, green or water area only up to
                    // its middle (Abs. 2); that rule is not checked yet.
                }
            }
        }

        /// <summary>The pieces of each curve outside all cutters.</summary>
        private static List<Curve> Difference(List<Curve> pieces, List<Curve> cutters, double tol)
        {
            foreach (Curve cutter in cutters)
            {
                pieces = pieces.SelectMany(c =>
                {
                    if (!Overlaps(c.GetBoundingBox(true), cutter.GetBoundingBox(true))) return new[] { c };
                    Curve flat = Flatten(cutter, c.PointAtStart.Z);
                    return Curve.CreateBooleanDifference(c, flat, tol) ?? Fallback(c, flat, tol);
                }).ToList();
            }
            return pieces;
        }

        /// <summary>If the curve boolean fails, keep the piece only when its centre is outside the cutter.</summary>
        private static Curve[] Fallback(Curve piece, Curve cutter, double tol)
        {
            Point3d mid = piece.GetBoundingBox(true).Center;
            return cutter.Contains(mid, Plane.WorldXY, tol) == PointContainment.Inside ? Array.Empty<Curve>() : new[] { piece };
        }

        private static void AddFlag(AfResult r, FlagKind kind, Curve region, double minArea, double metre, string label, string why, params int[] buildings)
        {
            AreaMassProperties amp = AreaMassProperties.Compute(region);
            if (amp == null || amp.Area < minArea) return;
            r.Flags.Add(new Flag
            {
                Kind = kind,
                Region = region,
                DotAt = amp.Centroid,
                Area = amp.Area / (metre * metre),
                Text = label,
                Why = why,
                Buildings = buildings.Distinct().ToArray(),
            });
        }

        private static string M(double v, double metre) => (v / metre).ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>Why two faces' Abstandsflächen may not overlap, with the numbers (§ 6 Abs. 3).</summary>
        private static string OverlapWhy(AfResult r, AfFace a, AfFace b, Curve overlap, AfRules rules, double metre)
        {
            double angle = AngleBetween(a, b);
            string limit = rules.OverlapExemptDeg.ToString("0.#", CultureInfo.InvariantCulture);
            if (angle < 5 && a.Outward * b.Outward < 0)
            {
                // Facing walls: the two walls whose areas make this overlap, how far apart they are and how deep.
                Point3d at = AreaMassProperties.Compute(overlap)?.Centroid ?? overlap.PointAtStart;
                Wall wa = WallAt(a, at), wb = WallAt(b, at);
                double apart = Math.Abs((wa.Start - wb.Start) * wa.Outward);
                string whose = a.Building == b.Building ? $"two walls of {r.Buildings[a.Building].Name}" : $"walls of {r.Buildings[a.Building].Name} and {r.Buildings[b.Building].Name}";
                return $"§ 6 Abs. 3: Abstandsflächen must not overlap. Here {whose} face each other {M(apart, metre)} m apart, but their " +
                       $"Abstandsflächen are {M(wa.Depth, metre)} m and {M(wb.Depth, metre)} m deep, {M(wa.Depth + wb.Depth, metre)} m together.";
            }
            return $"§ 6 Abs. 3: Abstandsflächen may only overlap where the walls stand at more than {limit}° to each other; these walls are at {angle:0}°.";
        }

        /// <summary>The wall of a face whose area holds a point (else the nearest one).</summary>
        private static Wall WallAt(AfFace f, Point3d p)
        {
            Point3d flat = new Point3d(p.X, p.Y, f.Walls[0].Start.Z);
            Wall inside = f.Walls.FirstOrDefault(w => w.Area.ToPolylineCurve().Contains(flat, Plane.WorldXY, 1e-6) != PointContainment.Outside);
            return inside ?? f.Walls.OrderBy(w => new Line(w.Start, w.End).DistanceTo(flat, true)).First();
        }

        // ---- roads -------------------------------------------------------------------------

        /// <summary>
        /// HBauO § 6 Abs. 2: Abstandsflächen may lie on public roads, but only up to their middle. The part of an
        /// Abstandsfläche past a road centre line (on the side away from its wall) is a clash. One that ends within
        /// 0.5 m before the centre line is listed to check, since the line may be drawn roughly.
        /// </summary>
        private static void CheckRoads(AfResult r, IList<Curve> roads, double tol, double minArea, double metre)
        {
            if (roads == null || roads.Count == 0) return;
            double near = 0.5 * metre;
            foreach (AfFace f in r.Faces)
            {
                string name = r.Buildings[f.Building].Name;
                Point3d wall = (f.Walls[0].Start + f.Walls[0].End) / 2;
                foreach (Curve area in f.Regions)
                {
                    double z = area.PointAtStart.Z;
                    BoundingBox abox = area.GetBoundingBox(true);
                    var plane = new Plane(new Point3d(0, 0, z), Vector3d.ZAxis);
                    foreach (Curve road0 in roads)
                    {
                        Curve road = Flatten(road0, z);
                        BoundingBox rbox = road.GetBoundingBox(true);
                        rbox.Inflate(near);
                        if (!Overlaps(abox, rbox)) continue;

                        CurveIntersections xs = Intersection.CurveCurve(area, road, tol, tol);
                        if (xs != null && xs.Count > 0)
                        {
                            int wallSide = Side(road, wall);
                            using (CurveBooleanRegions regions = Curve.CreateBooleanRegions(new[] { area, road }, plane, false, tol))
                            {
                                if (regions == null) continue;
                                for (int i = 0; i < regions.RegionCount; i++)
                                {
                                    Curve[] boundary = regions.RegionCurves(i);
                                    if (boundary == null || boundary.Length == 0) continue;
                                    Curve piece = boundary[0];
                                    AreaMassProperties amp = AreaMassProperties.Compute(piece);
                                    if (amp == null || Side(road, amp.Centroid) == wallSide) continue;
                                    double past = Far(piece, road);
                                    AddFlag(r, FlagKind.PastRoadCentre, piece, minArea, metre, $"{name} past road centre",
                                        $"§ 6 Abs. 2: an Abstandsfläche may cover a public road only up to its middle, but {name}'s goes " +
                                        $"{M(past, metre)} m past this road's centre line.", f.Building);
                                }
                            }
                        }
                        else if (area.ClosestPoints(road, out Point3d pa, out Point3d pb) && pa.DistanceTo(pb) < near &&
                                 Side(road, pa) == Side(road, wall))
                        {
                            r.Flags.Add(new Flag
                            {
                                Kind = FlagKind.NearRoadCentre,
                                IsCheck = true,
                                Region = area,
                                DotAt = pa,
                                Text = $"{name} close to road centre",
                                Why = $"{name}'s Abstandsfläche ends {M(pa.DistanceTo(pb), metre)} m before a road's centre line. That is allowed " +
                                      "(§ 6 Abs. 2), but close: check that the centre line is drawn accurately.",
                                Buildings = new[] { f.Building },
                            });
                        }
                    }
                }
            }
        }

        /// <summary>Which side of a curve a point is on in plan: +1 left, -1 right.</summary>
        private static int Side(Curve road, Point3d p)
        {
            road.ClosestPoint(p, out double t);
            Vector3d tangent = road.TangentAt(t);
            Vector3d to = p - road.PointAt(t);
            return tangent.X * to.Y - tangent.Y * to.X >= 0 ? 1 : -1;
        }

        /// <summary>The largest distance from points of a curve to another curve.</summary>
        private static double Far(Curve piece, Curve road)
        {
            double best = 0;
            foreach (Point3d p in piece.DivideEquidistant(Math.Max(piece.GetLength() / 50, 1e-6)) ?? Array.Empty<Point3d>())
            {
                if (road.ClosestPoint(p, out double t)) best = Math.Max(best, p.DistanceTo(road.PointAt(t)));
            }
            return best;
        }

        /// <summary>
        /// Plan outline of all of a building's volumes together (upper storeys that overhang count too): each
        /// volume's outline from above, joined into one.
        /// </summary>
        private static List<Curve> Footprint(List<Brep> volumes, double tol)
        {
            double z = volumes.Min(b => b.GetBoundingBox(true).Min.Z);
            var plane = new Plane(new Point3d(0, 0, z), Vector3d.ZAxis);
            var outlines = new List<Curve>();
            foreach (Brep brep in volumes)
            {
                var mesh = new Mesh();
                foreach (Mesh m in Mesh.CreateFromBrep(brep, MeshingParameters.FastRenderMesh) ?? Array.Empty<Mesh>()) mesh.Append(m);
                foreach (Polyline p in mesh.GetOutlines(plane) ?? Array.Empty<Polyline>()) outlines.Add(p.ToPolylineCurve());
            }
            if (outlines.Count < 2) return outlines;
            Curve[] union = Curve.CreateBooleanUnion(outlines, tol);
            return union != null && union.Length > 0 ? union.ToList() : outlines;
        }

        private static Curve Flatten(Curve c, double z)
        {
            Curve copy = c.DuplicateCurve();
            copy.Transform(Transform.PlanarProjection(new Plane(new Point3d(0, 0, z), Vector3d.ZAxis)));
            return copy;
        }

        private static bool Overlaps(BoundingBox a, BoundingBox b) =>
            a.Min.X <= b.Max.X && b.Min.X <= a.Max.X && a.Min.Y <= b.Max.Y && b.Min.Y <= a.Max.Y;
    }
}
