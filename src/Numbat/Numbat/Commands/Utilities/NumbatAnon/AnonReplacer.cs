using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Numbat.Commands.Utilities.NumbatAnon
{
    /// <summary>What a found phrase is replaced with.</summary>
    internal enum ReplaceMode
    {
        /// <summary>Random characters such as "QX7KMR", of a chosen length (one character upwards).</summary>
        RandomLetters,
        /// <summary>A random neutral word such as "Heron".</summary>
        RandomWord,
        /// <summary>Text the user types.</summary>
        Text,
        /// <summary>Nothing: the phrase is taken out. Names that would end up empty get a random code instead.</summary>
        Remove,
    }

    /// <summary>
    /// Finds one or more phrases in text (ignoring case unless asked, optionally whole words only) and replaces each
    /// with its own replacement. Longer phrases win where they overlap, so "ACME Architects" is replaced as a whole
    /// before "ACME" on its own.
    /// </summary>
    internal sealed class AnonMatcher
    {
        private readonly Regex _regex;

        public AnonMatcher(IEnumerable<string> phrases, bool matchCase, bool wholeWord)
        {
            StringComparer same = matchCase ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            Phrases = (phrases ?? Enumerable.Empty<string>())
                .Select(p => (p ?? string.Empty).Trim())
                .Where(p => p.Length > 0)
                .Distinct(same)
                .ToList();
            // One named group per phrase, longest first, so the regex prefers the longest phrase at each position.
            string pattern = string.Join("|", Phrases
                .Select((p, i) => (p, i))
                .OrderByDescending(x => x.p.Length)
                .Select(x => $"(?<p{x.i}>{Regex.Escape(x.p)})"));
            if (pattern.Length == 0) pattern = "(?!)"; // matches nothing
            // Word boundaries that also work next to umlauts and digits, and with phrases that start or end in
            // punctuation: "not preceded / followed by a letter or digit".
            else if (wholeWord) pattern = $@"(?<![\p{{L}}\p{{N}}])(?:{pattern})(?![\p{{L}}\p{{N}}])";
            _regex = new Regex(pattern, (matchCase ? RegexOptions.None : RegexOptions.IgnoreCase) | RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// Splits what the user typed into phrases: separated by commas (or new lines), trimmed, empty ones dropped.
        /// </summary>
        public static List<string> Split(string typed) =>
            (typed ?? string.Empty).Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim()).Where(p => p.Length > 0).ToList();

        public IReadOnlyList<string> Phrases { get; }

        /// <summary>The phrases quoted and listed, for messages: "ACME", "Hamburg".</summary>
        public string Label => string.Join(", ", Phrases.Select(p => $"\"{p}\""));

        public bool IsMatch(string text) => !string.IsNullOrEmpty(text) && Phrases.Count > 0 && _regex.IsMatch(text);

        /// <summary>Replaces every phrase with <paramref name="with"/>[its index in <see cref="Phrases"/>].</summary>
        public string Replace(string text, IReadOnlyList<string> with)
        {
            if (!IsMatch(text)) return text;
            return _regex.Replace(text, m =>
            {
                for (int i = 0; i < Phrases.Count; i++)
                {
                    if (m.Groups["p" + i].Success) return i < with.Count ? with[i] ?? string.Empty : string.Empty;
                }
                return m.Value;
            });
        }
    }

    /// <summary>
    /// The replacements for one run. Each phrase gets its own (two phrases never get the same random word or code).
    /// With <see cref="PerPlace"/>, every place gets its own set as well, like code words; otherwise a phrase becomes
    /// the same thing everywhere, so the layer, the block and the note still match afterwards.
    /// </summary>
    internal sealed class AnonReplacements
    {
        private readonly HashSet<string> _used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public AnonReplacements(ReplaceMode mode, int length, bool perPlace, string text, int phraseCount)
        {
            Mode = mode;
            Length = Math.Max(1, length);
            PerPlace = perPlace && IsRandom;
            Text = text ?? string.Empty;
            Shared = NewSet(phraseCount);
        }

        public ReplaceMode Mode { get; }
        public int Length { get; }
        public bool PerPlace { get; }
        public string Text { get; }

        public bool IsRandom => Mode == ReplaceMode.RandomLetters || Mode == ReplaceMode.RandomWord;

        /// <summary>The replacement of each phrase, when every place uses the same ones.</summary>
        public IReadOnlyList<string> Shared { get; }

        /// <summary>The replacements for one place: its own new set with <see cref="PerPlace"/>, else <see cref="Shared"/>.</summary>
        public IReadOnlyList<string> ForPlace() => PerPlace ? NewSet(Shared.Count) : Shared;

        /// <summary>A random code of <see cref="Length"/> characters, for names that would end up empty.</summary>
        public string Fallback() => AnonWords.Letters(Length);

        private string[] NewSet(int count)
        {
            var set = new string[count];
            for (int i = 0; i < count; i++) set[i] = One();
            return set;
        }

        private string One()
        {
            switch (Mode)
            {
                case ReplaceMode.Text: return Text;
                case ReplaceMode.Remove: return string.Empty;
            }
            // Avoid repeats while there are fresh ones to take: one-character codes run out after 32, words after 75.
            string next = null;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                next = Mode == ReplaceMode.RandomWord ? AnonWords.Word() : AnonWords.Letters(Length);
                if (_used.Add(next)) return next;
            }
            return next;
        }
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
    }
}
