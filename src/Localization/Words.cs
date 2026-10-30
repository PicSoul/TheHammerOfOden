namespace TheHammerOfOden
{
    /// <summary>
    /// The mod's own text, in the player's language. Keys live in translations/&lt;Language&gt;.yml;
    /// $1, $2 in a translation are filled from <paramref name="words"/>.
    /// </summary>
    internal static class Words
    {
        internal static string T(string key, params string[] words)
        {
            return Localization.instance != null ? Localization.instance.Localize(key, words) : key;
        }

        /// <summary>One or many, each language with its own wording.</summary>
        internal static string Count(int count, string oneKey, string manyKey)
        {
            return T(count == 1 ? oneKey : manyKey, count.ToString());
        }
    }
}
