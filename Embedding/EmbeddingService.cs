using Rimdex.Configuration;
using Rimdex.Data;
using Rimdex.Platform;

namespace Rimdex.Embedding;

internal sealed class EmbeddingService(ModRepository repository, EmbeddingClient client) {
    public async Task<int> EmbedAsync(EmbedOptions options, CancellationToken cancellationToken) {
        options.Validate();

        var config = RimdexConfig.Load();
        var pending = repository.ReadPendingEmbeddingRows(options.Limit, config.Model);
        if (pending.Count == 0) {
            Console.WriteLine("no pending mods to embed");
            return 0;
        }

        var embedded = 0;
        for (var index = 0; index < pending.Count; index += options.BatchSize) {
            var batch = pending.Skip(index).Take(options.BatchSize).ToArray();
            var vectors =
                await client.FetchAsync(batch.Select(row => row.SearchText).ToArray(), config, cancellationToken);
            var embeddings = new List<ModEmbedding>(batch.Length);

            for (var vectorIndex = 0; vectorIndex < vectors.Length; vectorIndex++) {
                embeddings.Add(new ModEmbedding(
                    batch[vectorIndex].ModId,
                    batch[vectorIndex].SearchTextHash,
                    EmbeddingVector.NormalizeToBlob(vectors[vectorIndex]),
                    vectors[vectorIndex].Length));
            }

            repository.UpsertEmbeddings(config.Model, embeddings);
            embedded += batch.Length;
            Console.WriteLine($"embedded {embedded}/{pending.Count}");
        }

        return embedded;
    }

    public static EmbeddingService Create() {
        return new EmbeddingService(
            new ModRepository(AppPaths.DatabasePath),
            new EmbeddingClient(new HttpClient()));
    }
}