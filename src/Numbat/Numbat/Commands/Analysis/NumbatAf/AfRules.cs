using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rhino;

namespace Numbat.Commands.Analysis.NumbatAf
{
    internal enum Zone
    {
        /// <summary>All areas except Gewerbe- and Industriegebiete.</summary>
        General,
        /// <summary>Gewerbe- and Industriegebiete (GE/GI).</summary>
        Commercial,
    }

    /// <summary>One editable number of the rule set, with its HBauO default and the paragraph it comes from.</summary>
    internal sealed class RuleField
    {
        public string Key;          // document string key
        public string Label;
        public string Paragraph;    // e.g. "§ 6 Abs. 5"
        public double Default;
        public double Min, Max;
        public Func<AfRules, double> Get;
        public Action<AfRules, double> Set;
        public Func<double, string> Format;

        public bool IsDefault(AfRules r) => Math.Abs(Get(r) - Default) < 1e-9;
    }

    /// <summary>
    /// Depth rules for Abstandsflächen. Defaults are Hamburg, HBauO § 6 (2025 text):
    /// Abs. 5: depth 0.4 H, at least 2.50 m; in GE/GI 0.2 H, at least 2.50 m.
    /// Abs. 4: roofs pitched under 70° add one third of their height to H, steeper roofs add their full height.
    /// Abs. 3: Abstandsflächen must not overlap, unless the walls are at more than 75° to each other.
    /// Every number can be changed per document (e.g. when a Bebauungsplan sets other values); the window shows
    /// which ones differ from the defaults.
    /// </summary>
    internal sealed class AfRules
    {
        public const string Source = "HBauO § 6 (Hamburg)";

        public Zone Zone { get; set; } = Zone.General;
        public double GeneralFactor { get; set; } = 0.4;
        public double CommercialFactor { get; set; } = 0.2;
        public double MinDepthM { get; set; } = 2.5;
        public double SteepRoofDeg { get; set; } = 70.0;
        public double ShallowRoofShare { get; set; } = 1.0 / 3.0;
        public double OverlapExemptDeg { get; set; } = 75.0;

        /// <summary>Volumes closer than this (mm) count as touching. A model setting, not part of HBauO.</summary>
        public double JoinGapMm { get; set; } = 10;

        /// <summary>Gaps between buildings under this (m) are reported as near-misses.</summary>
        public const double NearMissM = 0.2;

        /// <summary>The join gap as an editable value (shown in the window's MODEL section, not under RULE VALUES).</summary>
        public static readonly RuleField JoinGapField = new RuleField
        {
            Key = "joinGap", Label = "Join gaps under", Paragraph = "volumes this close are one building", Default = 10, Min = 0, Max = 199,
            Get = r => r.JoinGapMm, Set = (r, v) => r.JoinGapMm = v, Format = v => $"{N(v, "0.#")} mm",
        };

        public const string RoofShareKey = "roofShare";

        public static readonly IReadOnlyList<RuleField> Fields = new[]
        {
            new RuleField
            {
                Key = "factorGeneral", Label = "Depth factor", Paragraph = "§ 6 Abs. 5", Default = 0.4, Min = 0.01, Max = 3,
                Get = r => r.GeneralFactor, Set = (r, v) => r.GeneralFactor = v, Format = v => $"{N(v, "0.###")} H",
            },
            new RuleField
            {
                Key = "factorCommercial", Label = "Depth factor GE/GI", Paragraph = "§ 6 Abs. 5", Default = 0.2, Min = 0.01, Max = 3,
                Get = r => r.CommercialFactor, Set = (r, v) => r.CommercialFactor = v, Format = v => $"{N(v, "0.###")} H",
            },
            new RuleField
            {
                Key = "minDepth", Label = "Minimum depth", Paragraph = "§ 6 Abs. 5", Default = 2.5, Min = 0, Max = 100,
                Get = r => r.MinDepthM, Set = (r, v) => r.MinDepthM = v, Format = v => $"{N(v, "0.00")} m",
            },
            new RuleField
            {
                Key = "steepRoof", Label = "Roof counts fully from", Paragraph = "§ 6 Abs. 4", Default = 70, Min = 0, Max = 90,
                Get = r => r.SteepRoofDeg, Set = (r, v) => r.SteepRoofDeg = v, Format = v => $"{N(v, "0.#")}°",
            },
            new RuleField
            {
                Key = RoofShareKey, Label = "Share of flatter roofs", Paragraph = "§ 6 Abs. 4", Default = 1.0 / 3.0, Min = 0, Max = 1,
                Get = r => r.ShallowRoofShare, Set = (r, v) => r.ShallowRoofShare = v,
                Format = v => Math.Abs(v - 1.0 / 3.0) < 1e-6 ? "1/3" : N(v, "0.###"),
            },
            new RuleField
            {
                Key = "overlapAngle", Label = "Overlap allowed above", Paragraph = "§ 6 Abs. 3", Default = 75, Min = 0, Max = 90,
                Get = r => r.OverlapExemptDeg, Set = (r, v) => r.OverlapExemptDeg = v, Format = v => $"{N(v, "0.#")}°",
            },
        };

        public static RuleField Field(string key) => Fields.First(f => f.Key == key);

        public double Factor => Zone == Zone.Commercial ? CommercialFactor : GeneralFactor;

        /// <summary>True when every value is the HBauO default (general zone).</summary>
        public bool IsDefault => Zone == Zone.General && Fields.All(f => f.IsDefault(this));

        /// <summary>Is this factor the one not used in the current zone?</summary>
        public bool Unused(RuleField f) =>
            (f.Key == "factorGeneral" && Zone == Zone.Commercial) || (f.Key == "factorCommercial" && Zone == Zone.General);

        /// <summary>Share of the roof height that counts towards H for a roof of this pitch.</summary>
        public double RoofShare(double pitchDeg) => pitchDeg < SteepRoofDeg ? ShallowRoofShare : 1.0;

        /// <summary>Depth in the same units as <paramref name="h"/>; <paramref name="metre"/> is one metre in those units.</summary>
        public double Depth(double h, double metre) => Math.Max(Factor * h, MinDepthM * metre);

        public string Describe() =>
            $"{Source}{(IsDefault ? "" : " (values changed)")}, {(Zone == Zone.Commercial ? "GE/GI" : "general")}: " +
            $"{N(Factor, "0.###")} H, at least {N(MinDepthM, "0.00")} m";

        public AfRules Clone() => (AfRules)MemberwiseClone();

        public static AfRules Load(RhinoDoc doc)
        {
            var r = new AfRules { Zone = Get(doc, "zone") == nameof(Zone.Commercial) ? Zone.Commercial : Zone.General };
            foreach (RuleField f in Fields.Concat(new[] { JoinGapField }))
            {
                if (double.TryParse(Get(doc, f.Key), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) &&
                    v >= f.Min && v <= f.Max)
                {
                    f.Set(r, v);
                }
            }
            return r;
        }

        public void Save(RhinoDoc doc)
        {
            doc.Strings.SetString(AfInfo.DocSection, "zone", Zone.ToString());
            foreach (RuleField f in Fields.Concat(new[] { JoinGapField }))
            {
                doc.Strings.SetString(AfInfo.DocSection, f.Key, f.Get(this).ToString("R", CultureInfo.InvariantCulture));
            }
        }

        public void ResetToDefaults()
        {
            Zone = Zone.General;
            foreach (RuleField f in Fields) f.Set(this, f.Default);
        }

        private static string Get(RhinoDoc doc, string key) => doc.Strings.GetValue(AfInfo.DocSection, key);

        internal static string N(double v, string format) => v.ToString(format, CultureInfo.InvariantCulture);
    }
}
