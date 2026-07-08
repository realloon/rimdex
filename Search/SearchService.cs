using Rimdex.Configuration;
using Rimdex.Data;
using Rimdex.Embedding;
using Rimdex.Platform;
using Rimdex.Serialization;
using System.Text.Json;

namespace Rimdex.Search;

internal sealed class SearchService(ModRepository repository, EmbeddingClient client) {
    public async Task<int> SearchAsync(SearchOptions options, CancellationToken cancellationToken) {
        options.Validate();

        var config = RimdexConfig.Load();
        var rows = repository.ReadSearchEmbeddingRows(config.Model);
        if (rows.Count == 0) {
            throw new InvalidOperationException($"No embeddings found for model: {config.Model}");
        }

        var queryVectors = await client.FetchAsync([options.Query], config, cancellationToken);
        var query = EmbeddingVector.Normalize(queryVectors[0]);

        var results = rows
            .Select(row => SearchRanker.Rank(row, options.Query,
                EmbeddingVector.CosineDistance(row.Embedding, row.Dimension, query)))
            .OrderBy(result => result.Distance)
            .Take(options.Candidates)
            .OrderBy(result => result.RankScore)
            .Take(options.Limit)
            .ToArray();

        PrintResults(results);

        return results.Length;
    }

    public static SearchService Create() {
        return new SearchService(
            new ModRepository(AppPaths.DatabasePath),
            new EmbeddingClient(new HttpClient()));
    }

    private static void PrintResults(IReadOnlyList<SearchResult> results) {
        var dto = results
            .Select(result => new SearchResultDto(
                result.Row.PublishedFileId,
                result.Row.Title,
                SearchRanker.Summarize(result.Row.Description),
                result.Row.PreviewUrl,
                result.Row.Subscriptions,
                result.Row.Views,
                result.Distance,
                result.RankScore))
            .ToArray();

        Console.WriteLine(JsonSerializer.Serialize(dto, RimdexIndentedJsonContext.Default.SearchResultDtoArray));
    }
}