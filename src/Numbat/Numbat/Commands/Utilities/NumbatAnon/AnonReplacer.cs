using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Numbat.Commands.Utilities.NumbatAnon
{
    /// <summary>What a found phrase is replaced with.</summary>
    internal enum ReplaceMode
    {
        /// <summary>A random code such as "QX7KMR".</summary>
        RandomLetters,
        /// <summary>A random neutral word such as "Heron".</summary>
        RandomWord,
        /// <summary>Text the user types.</summary>
        Text,
        /// <summary>Nothing: the phrase is taken out. Names that would end up empty get a random code instead.</summary>
        Remove,
    }

    /// <summary>
    /// Finds a phrase in text (ignoring case unless asked, optionally whole words only) and replaces it. The same
    /// replacement is used everywhere in one run, so "ACME" becomes the same word in every layer, block and note.
    /// </summary>
    internal sealed class AnonMatcher
    {
        private readonly Regex _regex;

        public AnonMatcher(string phrase, bool matchCase, bool wholeWord)
        {
            string pattern = Regex.Escape(phrase ?? string.Empty);
            // Word boundaries that also work next to umlauts and digits, and with phrases that start or end in
            // punctuation: "not preceded / followed by a letter or digit".
            if (wholeWord) pattern = $@"(?<![\p{{L}}\p{{N}}]){pattern}(?![\p{{L}}\p{{N}}])";
            _regex = new Regex(pattern, (matchCase ? RegexOptions.None : RegexOptions.IgnoreCase) | RegexOptions.CultureInvariant);
            Phrase = phrase;
        }

        public string Phrase { get; }

        public bool IsMatch(string text) => !string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(Phrase) && _regex.IsMatch(text);

        public int Count(string text) => IsMatch(text) ? _regex.Matches(text).Count : 0;

        public string Replace(string text, string with) => IsMatch(text) ? _regex.Replace(text, with ?? string.Empty) : text;
    }

    /// <summary>Random replacements: codes and neutral words.</summary>
    internal static class AnonWords
    {
        private static readonly Random Rng = new Random();

        // Neutral nouns that don't look like a company, a person or a place.
        private static readonly string[] Words =
        {
            "Alder", "Aspen", "Badger", "Basalt", "Beacon", "Birch", "Bramble", "Cedar", "Comet", "Copper",
            "Cove", "Crane", "Dune", "Ember", "Fable", "Falcon", "Fern", "Finch", "Fjord", "Flint",
            "Garnet", "Glade", "Granite", "Harbour", "Hazel", "Heron", "Holly", "Indigo", "Iris", "Ivy",
            "Jasper", "Juniper", "Kestrel", "Kiln", "Lagoon", "Larch", "Lark", "Linden", "Lotus", "Maple",
            "Marble", "Meadow", "Mistral", "Moss", "Nimbus", "Oak", "Onyx", "Orbit", "Osprey", "Otter",
            "Pebble", "Pine", "Plover", "Quartz", "Quill", "Raven", "Reed", "Ridge", "Rowan", "Saffron",
            "Sage", "Sequoia", "Slate", "Sparrow", "Spruce", "Summit", "Tamarack", "Thistle", "Tundra", "Umber",
            "Valley", "Willow", "Wren", "Yarrow", "Zephyr",
        };

        public static string Letters(int length = 6)
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no I/O/1/0, which read alike
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++) sb.Append(alphabet[Rng.Next(alphabet.Length)]);
            return sb.ToString();
        }

        public static string Word() => Words[Rng.Next(Words.Length)];

        /// <summary>A replacement for the chosen mode. <paramref name="text"/> is used for <see cref="ReplaceMode.Text"/>.</summary>
        public static string For(ReplaceMode mode, string text)
        {
            switch (mode)
            {
                case ReplaceMode.RandomLetters: return Letters();
                case ReplaceMode.RandomWord: return Word();
                case ReplaceMode.Text: return text ?? string.Empty;
                default: return string.Empty;
            }
        }
    }
}
