using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Numbat.Commands.Analysis.NumbatProgramBlocks
{
    internal enum ProgramTextMode
    {
        TextObject,
        TextDot
    }

    internal sealed class ProgramBlockRow
    {
        public string Name { get; set; }
        public double AreaSquareMetres { get; set; }
        public string Category { get; set; }
        public string CategoryName { get; set; }
    }

    internal sealed class ProgramBlocksOptions
    {
        public ProgramTextMode TextMode { get; set; } = ProgramTextMode.TextObject;
        public bool IncludeVolume { get; set; }
    }

    internal static class ProgramBlocksData
    {
        public const double RoomGapMetres = 1.0;
        public const double CategoryGapMetres = 1.0;
        public const double NameTextHeightMetres = 0.3375;
        public const double AreaTextHeightMetres = NameTextHeightMetres * 0.5;
        public const double VolumeHeightMetres = 3.5;
        public const double MinimumShortSideMetres = 2.4;
        public const double PreferredLongSideMetres = 6.0;
        public const double TargetAspect = 1.5;
        public const string MasterLayerName = "nbPROGRAM";

        public static readonly string[] CategoryColours =
        {
            "#ffadad",
            "#ffd6a5",
            "#fdffb6",
            "#caffbf",
            "#96e8ff",
            "#a0c4ff",
            "#bdb2ff",
            "#debcff",
            "#ffc6ff",
            "#ffa7dc"
        };

        public static string SafeName(string text)
        {
            string value = (text ?? string.Empty).Trim();
            value = Regex.Replace(value, "[<>:\"/\\\\|?*]", "_");
            value = Regex.Replace(value, "\\s+", " ");
            return string.IsNullOrWhiteSpace(value) ? "Unnamed" : value;
        }

        public static bool TryParseRows(
            string text,
            out List<ProgramBlockRow> rows,
            out List<string> errors)
        {
            rows = new List<ProgramBlockRow>();
            errors = new List<string>();

            string[] lines = (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                string[] columns = raw.Split('\t');
                if (columns.Length < 3)
                {
                    errors.Add($"Line {i + 1}: expected at least 3 tab-separated columns.");
                    continue;
                }

                string name = columns[0].Trim();
                string areaText = columns[1].Trim().Replace(',', '.');
                string category = columns[2].Trim();

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category))
                {
                    errors.Add($"Line {i + 1}: program name and category are required.");
                    continue;
                }

                if (!double.TryParse(areaText, NumberStyles.Float, CultureInfo.InvariantCulture, out double area))
                {
                    errors.Add($"Line {i + 1}: '{columns[1].Trim()}' is not a valid area.");
                    continue;
                }

                if (double.IsNaN(area) || double.IsInfinity(area) || area <= 0.0)
                {
                    errors.Add($"Line {i + 1}: area must be greater than 0.");
                    continue;
                }

                rows.Add(new ProgramBlockRow
                {
                    Name = name,
                    AreaSquareMetres = area,
                    Category = category,
                    CategoryName = SafeName(category)
                });
            }

            return errors.Count == 0 && rows.Count > 0;
        }

        public static void GetRoomDimensions(double areaSquareMetres, out double widthMetres, out double heightMetres)
        {
            double square = Math.Sqrt(areaSquareMetres);
            double longSide = Math.Sqrt(areaSquareMetres * TargetAspect);
            double shortSide = areaSquareMetres / longSide;

            if (longSide < PreferredLongSideMetres)
            {
                double candidateShort = areaSquareMetres / PreferredLongSideMetres;
                if (candidateShort >= MinimumShortSideMetres)
                {
                    longSide = PreferredLongSideMetres;
                    shortSide = candidateShort;
                }
                else
                {
                    longSide = square;
                    shortSide = square;
                }
            }

            widthMetres = longSide;
            heightMetres = shortSide;
        }
    }
}
