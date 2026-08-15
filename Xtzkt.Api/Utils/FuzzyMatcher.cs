namespace Xtzkt.Api.Utils;

/// <summary>
/// Ranks strings against a search query, from an exact match down to a fuzzy one.
/// The fuzzy tier is trigram similarity with the same semantics as `pg_trgm`, so that
/// in-memory results are ranked consistently with the ones coming from the DB.
/// </summary>
public class FuzzyMatcher
{
    /// <summary>Score of a target that doesn't match at all.</summary>
    public const double NoMatch = -1.0;

    /// <summary>Same default as `pg_trgm.similarity_threshold`.</summary>
    public const double Threshold = 0.3;

    /// <summary>Shorter queries have no full trigram, so they are matched by substring only.</summary>
    public const int MinFuzzyLength = 3;

    const double ExactScore = 1.0;
    const double PrefixScore = 0.9;
    const double ContainsScore = 0.7;

    const double FuzzyScale = 0.6;

    readonly string Query;
    readonly HashSet<string>? QueryTrigrams;

    public FuzzyMatcher(string query)
    {
        Query = query.ToLowerInvariant();
        QueryTrigrams = Query.Length >= MinFuzzyLength ? Trigrams(Query) : null;
    }

    /// <summary>Scores the target within [0..1], or <see cref="NoMatch"/> if it doesn't match.</summary>
    public double Score(FuzzyString target)
    {
        if (target.Lowered == Query)
            return ExactScore;

        if (target.Lowered.StartsWith(Query, StringComparison.Ordinal))
            return PrefixScore;

        if (target.Lowered.Contains(Query, StringComparison.Ordinal))
            return ContainsScore;

        if (QueryTrigrams == null)
            return NoMatch;

        var similarity = Similarity(QueryTrigrams, target.Trigrams);
        return similarity < Threshold ? NoMatch : similarity * FuzzyScale;
    }

    /// <summary>Jaccard index of two trigram sets, i.e. `pg_trgm.similarity()`.</summary>
    static double Similarity(HashSet<string> left, HashSet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
            return 0.0;

        var intersection = 0;
        foreach (var trigram in left)
            if (right.Contains(trigram))
                intersection++;

        return (double)intersection / (left.Count + right.Count - intersection);
    }

    /// <summary>Splits the value into trigrams the way `pg_trgm.show_trgm()` does.</summary>
    internal static HashSet<string> Trigrams(string value)
    {
        var res = new HashSet<string>();

        for (int i = 0; i < value.Length;)
        {
            if (!char.IsLetterOrDigit(value[i])) { i++; continue; }

            var start = i;
            while (i < value.Length && char.IsLetterOrDigit(value[i])) i++;

            // non-alphanumerics separate words, and each word is padded with two leading
            // and one trailing space, so that prefixes and suffixes weigh more
            var word = $"  {value.AsSpan(start, i - start)} ";
            for (int j = 0; j + 3 <= word.Length; j++)
                res.Add(word.Substring(j, 3));
        }

        return res;
    }
}

/// <summary>
/// A string in the form <see cref="FuzzyMatcher"/> consumes it. The derived form doesn't depend
/// on the query, so it's computed once up front rather than on every search.
/// </summary>
public class FuzzyString
{
    /// <summary>The string as it was given.</summary>
    public string Original { get; }

    /// <summary>Lowercased <see cref="Original"/>.</summary>
    public string Lowered { get; }

    /// <summary>Trigrams of <see cref="Lowered"/>.</summary>
    public HashSet<string> Trigrams { get; }

    public FuzzyString(string value)
    {
        Original = value;
        Lowered = value.ToLowerInvariant();
        Trigrams = FuzzyMatcher.Trigrams(Lowered);
    }
}
