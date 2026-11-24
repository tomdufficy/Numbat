using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Numbat.Commands.Analysis.NumbatAf
{
    /// <summary>A legal text nbAF's rules rely on: a saved copy (with the date it was saved) and the live link.</summary>
    internal sealed class AfSource
    {
        public string Key;      // command-line option name
        public string Title;
        public string File;     // in docs/sources, copied next to the plugin under "sources"; null = online only
        public string Url;
        public string Accessed; // date the copy was downloaded or the link checked (yyyy-MM-dd)
        public string Note;

        public string LocalPath => File == null ? null : Path.Combine(AfSources.Folder, File);

        public bool HasLocalCopy => LocalPath != null && System.IO.File.Exists(LocalPath);
    }

    /// <summary>
    /// The sources behind the rules in <see cref="AfRules"/>. Keep in sync with docs/sources/README.md.
    /// The saved copies cover the case where a page moves or changes; the links give the current version.
    /// </summary>
    internal static class AfSources
    {
        public static string Folder =>
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "sources");

        public static readonly IReadOnlyList<AfSource> All = new[]
        {
            new AfSource
            {
                Key = "HBauO", Title = "Hamburgische Bauordnung (HBauO) vom 6. Januar 2025, nichtamtliche Lesefassung, Stand 25.09.2025",
                File = "HBauO-2025-Lesefassung.pdf", Accessed = "2026-11-23",
                Url = "https://www.hamburg.de/resource/blob/1120174/a1f3778eb712e7a95605fbc6872df915/hamburgische-bauordnung-hbauo-2025-nichtamtliche-lesefassung-data.pdf",
                Note = "§ 6 Abstandsflächen. The rules nbAF implements.",
            },
            new AfSource
            {
                Key = "FAQ", Title = "Hamburg FAQ zu § 6 HBauO (Abstandsflächen)",
                File = "HBauO-FAQ-6-Abstandsflaechen.pdf", Accessed = "2026-11-23",
                Url = "https://www.hamburg.de/resource/blob/190284/553ed9c05d0bcf090c18643e534441e0/faq-06-hbauo-abstandsflaechen-data.pdf",
                Note = "The city's explanations of § 6. Written for an earlier version of the law.",
            },
            new AfSource
            {
                Key = "Model3D", Title = "LGV Hamburg: 3D-Stadtmodell (LoD1/LoD2 buildings)",
                Accessed = "2026-11-23",
                Url = "https://www.hamburg.de/politik-und-verwaltung/behoerden/behoerde-fuer-stadtentwicklung-und-wohnen/aemter-und-landesbetrieb/landesbetrieb-geoinformation-und-vermessung/produkte-und-dienstleistungen/geodaten-des-lgv/3d-stadtmodell-244116",
                Note = "Data source for neighbouring buildings (not imported yet).",
            },
            new AfSource
            {
                Key = "ALKIS", Title = "LGV Hamburg: ALKIS (parcels and footprints)",
                Accessed = "2026-11-23",
                Url = "https://www.hamburg.de/politik-und-verwaltung/behoerden/behoerde-fuer-stadtentwicklung-und-wohnen/aemter-und-landesbetrieb/landesbetrieb-geoinformation-und-vermessung/produkte-und-dienstleistungen/geodaten-des-lgv/alkis-244248",
                Note = "Data source for plot boundaries (not imported yet).",
            },
        };

        /// <summary>Opens a file or web page with the system's default program.</summary>
        public static void Open(string target)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (System.Exception e)
            {
                Rhino.RhinoApp.WriteLine($"{AfInfo.Name}: could not open {target}: {e.Message}");
            }
        }
    }
}
