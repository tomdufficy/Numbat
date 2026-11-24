using System;
using System.Collections.Generic;

namespace Numbat.UI
{
    /// <summary>
    /// Which tool's viewport preview is shown, so the colours of several open tools (nbGFA's uses, nbAF's areas and
    /// building tints) never mix. One tool at a time: the window opened (or brought back) most recently shows its
    /// preview, the others' previews are hidden. Each window has an eye in its header: clicking a closed eye shows
    /// that tool's preview and hides the current one; clicking the open eye hides it, so no preview is shown.
    /// </summary>
    internal static class KitPreview
    {
        // Open tools, most recent last.
        private static readonly List<string> Open = new List<string>();

        /// <summary>The tool whose preview is shown, or null for none.</summary>
        public static string Shown { get; private set; }

        /// <summary>Raised when the shown tool changes. Tools turn their preview on or off and repaint their eye.</summary>
        public static event EventHandler Changed;

        public static bool IsShown(string tool) => tool != null && tool == Shown;

        /// <summary>A tool's window opened or came back to the front: it becomes the one shown.</summary>
        public static void Opened(string tool)
        {
            Open.Remove(tool);
            Open.Add(tool);
            Set(tool);
        }

        /// <summary>A tool's window closed. If its preview was shown, the most recent other open tool takes over.</summary>
        public static void Closed(string tool)
        {
            Open.Remove(tool);
            if (Shown == tool) Set(Open.Count > 0 ? Open[Open.Count - 1] : null);
        }

        /// <summary>The eye was clicked: show this tool's preview, or hide it if it is the one shown.</summary>
        public static void Toggle(string tool) => Set(Shown == tool ? null : tool);

        private static void Set(string tool)
        {
            if (Shown == tool) return;
            Shown = tool;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>The eye's hover tip for a tool, given its display name ("nbAF").</summary>
        public static string Tip(string tool, string name) => IsShown(tool)
            ? $"{name}'s colours are shown in the viewports. Click to hide them."
            : Shown != null
                ? $"{name}'s colours are hidden, so they don't mix with another tool's. Click to show them instead."
                : $"{name}'s colours are hidden. Click to show them.";
    }
}
