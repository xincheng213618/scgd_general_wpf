namespace ColorVision.UI.Serach;

/// <summary>Deterministic, side-effect-free ranking shared by static and provider-produced results.</summary>
public static class SearchQuery
{
    public static IReadOnlyList<SearchResultItem> MatchAndRank(IEnumerable<SearchResultItem> staticItems,
        IEnumerable<SearchResultItem> dynamicItems, string query, int limit = 60,
        string? category = null, IReadOnlyList<string>? recentIds = null, int perSourceLimit = 20)
    {
        if (limit <= 0 || perSourceLimit <= 0) return [];
        string[] terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var candidates = staticItems.Select(item => (Item: item, ProviderMatched: false))
            .Concat(dynamicItems.Select(item => (Item: item, ProviderMatched: true)))
            .Where(entry => string.IsNullOrEmpty(category) || string.Equals(entry.Item.CategoryKey, category, StringComparison.OrdinalIgnoreCase))
            .Select((entry, index) => (entry.Item, Index: index, Score: Score(entry.Item, terms, entry.ProviderMatched, recentIds)))
            .Where(entry => entry.Score >= 0)
            .OrderBy(entry => entry.Item.CategoryKey == "External")
            .ThenByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Index);

        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var results = new List<SearchResultItem>();
        foreach (var entry in candidates)
        {
            int count = counts.GetValueOrDefault(entry.Item.ProviderId);
            if (count >= perSourceLimit || !identities.Add(entry.Item.StableId)) continue;
            counts[entry.Item.ProviderId] = count + 1;
            results.Add(entry.Item);
            if (results.Count >= limit) break;
        }
        return results;
    }

    private static int Score(SearchResultItem item, string[] terms, bool providerMatched, IReadOnlyList<string>? recentIds)
    {
        int score = 0;
        foreach (string term in terms)
        {
            int termScore = Match(item.Title, term, 1000, 800, 600);
            foreach (string alias in item.Aliases) termScore = Math.Max(termScore, Match(alias, term, 500, 450, 400));
            termScore = Math.Max(termScore, Match(item.Description, term, 250, 225, 200));
            termScore = Math.Max(termScore, Match(item.Category, term, 150, 140, 130));
            termScore = Math.Max(termScore, Match(item.ShortcutText, term, 150, 140, 130));
            termScore = Math.Max(termScore, Match(item.Source.GuidId ?? string.Empty, term, 100, 90, 80));
            if (termScore == 0 && !providerMatched) return -1;
            score += termScore;
        }
        if (terms.Length > 0 && string.Equals(item.Title, string.Join(" ", terms), StringComparison.OrdinalIgnoreCase)) score += 2000;
        if (recentIds != null)
        {
            for (int i = 0; i < Math.Min(10, recentIds.Count); i++)
            {
                if (!string.Equals(recentIds[i], item.StableId, StringComparison.OrdinalIgnoreCase)) continue;
                score += 50 - i;
                break;
            }
        }
        if (terms.Length == 0 && !string.IsNullOrEmpty(item.ShortcutText)) score += 10;
        return score;
    }

    private static int Match(string value, string term, int exact, int prefix, int contains)
        => string.Equals(value, term, StringComparison.OrdinalIgnoreCase) ? exact
        : value.StartsWith(term, StringComparison.OrdinalIgnoreCase) ? prefix
        : value.Contains(term, StringComparison.OrdinalIgnoreCase) ? contains : 0;
}
