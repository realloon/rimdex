using System.Text.RegularExpressions;
using Rimdex.Data;

namespace Rimdex.Search;

internal static partial class SearchRanker {
    public static SearchResult Rank(SearchModRow row, string query, float distance) {
        var terms = QueryTerms(query);
        var text = $"{row.Title}\n{row.Description}".ToLowerInvariant();
        var title = row.Title.ToLowerInvariant();
        var titleHits = terms.Count(title.Contains);
        var textHits = terms.Count(text.Contains);
        var popularityBoost =
            Math.Log10(row.Subscriptions + 1) * 0.015 +
            Math.Log10(row.Views + 1) * 0.005;
        var lexicalBoost = titleHits * 0.04 + textHits * 0.01;
        var translationPenalty = IsLikelyTranslation(row) ? 0.08 : 0;

        return new SearchResult(
            row,
            distance,
            distance - popularityBoost - lexicalBoost + translationPenalty);
    }

    public static string Summarize(string value) {
        var normalized = WhitespacePattern().Replace(value, " ").Trim();
        return normalized.Length > 240
            ? $"{normalized[..237].TrimEnd()}..."
            : normalized;
    }

    private static List<string> QueryTerms(string query) {
        return QueryTermSplitter()
            .Split(query.ToLowerInvariant())
            .Where(term => term.Length >= 3)
            .ToList();
    }

    private static bool IsLikelyTranslation(SearchModRow row) {
        var title = row.Title.ToLowerInvariant();
        return TranslationTerms().Any(title.Contains);
    }

    private static string[] TranslationTerms() {
        return [
            "translation",
            "translated",
            "翻译",
            "汉化",
            "中文",
            "漢化",
            "zh",
            "日本語",
            "перевод",
            "рус",
            "한국어"
        ];
    }

    [GeneratedRegex(@"[^a-z0-9\u3400-\u9fff]+", RegexOptions.CultureInvariant)]
    private static partial Regex QueryTermSplitter();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();
}