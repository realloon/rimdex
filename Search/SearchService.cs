using Rimdex.Configuration;
using Rimdex.Data;
using Rimdex.Embedding;
using Rimdex.Platform;
using Rimdex.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Rimdex.Search;

internal sealed partial class SearchService(ModRepository repository, EmbeddingClient client) {
    public async Task<int> SearchAsync(SearchOptions options, CancellationToken cancellationToken) {
        options.Validate();

        var results = options.Keyword
            ? repository.SearchKeywords(options.Query, options.Limit)
            : await SearchSemanticAsync(options.Query, options.Limit, cancellationToken);

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
        var normalizedQuery = EmbeddingVector.Normalize(queryVectors[0]);
        return repository.SearchSemantic(
            config.Model,
            EmbeddingVector.ToBlob(normalizedQuery),
            limit);
    }

    public static SearchService Create() {
        return new SearchService(
            new ModRepository(AppPaths.DatabasePath),
            new EmbeddingClient(new HttpClient()));
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

        Console.WriteLine(JsonSerializer.Serialize(dto, RimdexIndentedJsonContext.Console.SearchResultDtoArray));
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