using Numbat.UI;

namespace Numbat.Commands.Analysis.NumbatGfa
{
    /// <summary>Shows docs/nbGFA.md (embedded in the plugin) in the kit's guide window.</summary>
    internal static class HelpWindow
    {
        public static void ShowWindow() =>
            KitHelpWindow.Show($"{GfaInfo.Title} · Guide", "nbGFA.md", GfaInfo.Copyright);
    }
}
