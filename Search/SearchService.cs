using Rimdex.Configuration;
using Rimdex.Data;
using Rimdex.Embedding;
using Rimdex.Platform;
using Rimdex.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Rimdex.Search;

internal sealed record SearchResultDto(
    string Url,
    string Title,
    string Summary,
    string PreviewUrl,
    long Subscriptions,
    long Views);

internal sealed partial class SearchService(ModRepository repository, EmbeddingClient client) {
    public async Task<int> SearchAsync(string query, int limit, bool keyword, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(query)) {
            throw new ArgumentException("query must not be empty", nameof(query));
        }

        if (limit <= 0) {
            throw new ArgumentOutOfRangeException(nameof(limit), "limit must be positive");
        }

        var results = keyword
            ? repository.SearchKeywords(query, limit)
            : await SearchSemanticAsync(query, limit, cancellationToken);

        PrintResults(results);
        return 0;
    }

    private async Task<IReadOnlyList<SearchResultRow>> SearchSemanticAsync(
        string query,
        int limit,
        CancellationToken cancellationToken) {
        var config = RimdexConfig.Load();
        repository.EnsureSearchIndex(config.Model);
        var queryVectors = await client.FetchAsync([query], config, cancellationToken);
        return repository.SearchSemantic(config.Model, EmbeddingVector.ToBlob(queryVectors[0]), limit);
    }

    public static SearchService Create() {
        return new SearchService(new ModRepository(AppPaths.DatabasePath), new EmbeddingClient(new HttpClient()));
    }

    private static void PrintResults(IReadOnlyList<SearchResultRow> results) {
        var dto = results
            .Select(result => new SearchResultDto(
                $"https://steamcommunity.com/sharedfiles/filedetails/?id={result.PublishedFileId}",
                result.Title,
                Summarize(result.Description),
                result.PreviewUrl,
                result.Subscriptions,
                result.Views))
            .ToArray();

        Console.WriteLine(JsonSerializer.Serialize(dto, RimdexJsonContext.Indented.SearchResultDtoArray));
    }

    private static string Summarize(string value) {
        var normalized = WhitespacePattern().Replace(value, " ").Trim();
        return normalized.Length > 240
            ? $"{normalized[..237].TrimEnd()}..."
            : normalized;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();
}