using System.Collections.Generic;
using System.Linq;

namespace Numbat.UI
{
    /// <summary>
    /// Which sections of a kit window are closed, remembered per user under one settings key per window
    /// (for example "af.closed"). Sections are open unless the user closed them.
    /// </summary>
    internal sealed class KitSections
    {
        private readonly string _key;
        private readonly HashSet<string> _closed;

        public KitSections(string settingsKey)
        {
            _key = settingsKey;
            string saved = NumbatPlugin.Instance?.Settings.GetString(_key, string.Empty) ?? string.Empty;
            _closed = new HashSet<string>(saved.Split(',').Where(s => s.Length > 0));
        }

        public bool IsOpen(string section) => !_closed.Contains(section);

        public void Toggle(string section)
        {
            if (!_closed.Remove(section)) _closed.Add(section);
            NumbatPlugin.Instance?.Settings.SetString(_key, string.Join(",", _closed));
        }
    }
}
