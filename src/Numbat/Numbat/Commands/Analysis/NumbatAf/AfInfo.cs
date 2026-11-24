namespace Numbat.Commands.Analysis.NumbatAf
{
    /// <summary>
    /// Every name nbAF stores in documents or shows to users.
    /// Changing a key means documents saved with the old key are no longer read.
    /// </summary>
    internal static class AfInfo
    {
        /// <summary>Name used in command-line messages and window titles.</summary>
        public const string Name = "nbAF";

        /// <summary>Title bar text: command name and what it does.</summary>
        public const string Title = "nbAF – Abstandsflächen";

        public const string Copyright = "© 2026 Tom Dufficy";

        /// <summary>Document string-table section for the rule values.</summary>
        public const string DocSection = "numbat.af";

        /// <summary>Parent layer for baked output (nbAF::Areas, nbAF::Clashes, ...).</summary>
        public const string OutputLayer = "nbAF";

        /// <summary>Volume user string marking the volumes nbAF tracks (any value; nbAF writes "tracked").</summary>
        public const string RoleKey = "numbat.af.role";

        /// <summary>Volume user string: id of the building's plot boundary curve.</summary>
        public const string PlotKey = "numbat.af.plot";

        /// <summary>Prefix for nbAF's per-user plugin settings (window position, colours, export options).</summary>
        public const string SettingsPrefix = "af.";
    }
}
