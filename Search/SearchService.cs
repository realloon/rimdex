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
        if (!repository.HasSearchVectors(config.Model)) {
            throw new InvalidOperationException($"No embeddings found for model: {config.Model}");
        }

        var queryVectors = await client.FetchAsync([options.Query], config, cancellationToken);
        var query = EmbeddingVector.Normalize(queryVectors[0]);
        var candidates = FindNearestCandidates(
            repository.ReadSearchVectorRows(config.Model), query, options.Candidates);
        var mods = repository.ReadSearchModRows(candidates.Select(candidate => candidate.ModId));

        var results = candidates
            .Select(candidate => SearchRanker.Rank(mods[candidate.ModId], options.Query, candidate.Distance))
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

    private static IReadOnlyList<VectorCandidate> FindNearestCandidates(
        IEnumerable<SearchVectorRow> rows,
        float[] query,
        int count) {
        var queue = new PriorityQueue<VectorCandidate, float>();
        foreach (var row in rows) {
            var distance = EmbeddingVector.CosineDistance(row.Embedding, row.Dimension, query);
            queue.Enqueue(new VectorCandidate(row.ModId, distance), -distance);
            if (queue.Count > count) {
                queue.Dequeue();
            }
        }

        var candidates = new List<VectorCandidate>(queue.Count);
        while (queue.TryDequeue(out var candidate, out _)) {
            candidates.Add(candidate);
        }

        candidates.Sort((left, right) => left.Distance.CompareTo(right.Distance));
        return candidates;
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

    private readonly record struct VectorCandidate(int ModId, float Distance);
}