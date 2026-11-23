namespace Numbat.Commands.Analysis.NumbatGfa
{
    /// <summary>
    /// Every name nbGFA stores in documents or shows to users.
    /// Changing a key means documents saved with the old key are no longer read.
    /// </summary>
    internal static class GfaInfo
    {
        /// <summary>Name used in command-line messages and window titles.</summary>
        public const string Name = "nbGFA";

        public const string Copyright = "© 2026 Tom Dufficy";

        /// <summary>Top-level layer used in By layer mode (nbGFA::Residential, nbGFA::Unassigned, ...).</summary>
        public const string RootLayer = "nbGFA";

        /// <summary>Object user-string key that holds a volume's use (e.g. "Residential").</summary>
        public const string UseKey = "numbat.gfa.use";

        /// <summary>
        /// Object user-string key holding the id of the layer a volume was on before By layer mode moved it onto
        /// the nbGFA layers, so excluding it or switching back to By object can put it back.
        /// </summary>
        public const string HomeLayerKey = "numbat.gfa.home";

        /// <summary>Object user-string key that excludes a volume from GFA tracking.</summary>
        public const string IgnoreKey = "numbat.gfa.ignore";

        /// <summary>Document string-table section for nbGFA settings.</summary>
        public const string DocSection = "numbat.gfa";

        /// <summary>Entry in <see cref="DocSection"/> holding the use categories and their limits.</summary>
        public const string CategoriesEntry = "categories";
    }
}
