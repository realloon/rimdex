using System.Runtime.InteropServices;
using Rimdex.Configuration;
using Rimdex.Data;
using Rimdex.Platform;

namespace Rimdex.Embedding;

internal static class EmbeddingVector {
    public static byte[] ToBlob(float[] vector) {
        if (vector.Length == 0) {
            throw new InvalidDataException("Embedding API returned an empty vector");
        }

        double sum = 0;
        foreach (var value in vector) {
            if (!float.IsFinite(value)) {
                throw new InvalidDataException("Embedding API returned a non-finite vector value");
            }

            sum += (double)value * value;
        }

        var norm = (float)Math.Sqrt(sum);
        if (norm == 0 || !float.IsFinite(norm)) {
            throw new InvalidDataException("Embedding API returned an invalid vector norm");
        }

        var normalized = new float[vector.Length];
        for (var i = 0; i < vector.Length; i++) {
            normalized[i] = vector[i] / norm;
        }

        return MemoryMarshal.AsBytes(normalized.AsSpan()).ToArray();
    }
}

internal sealed class EmbeddingService(ModRepository repository, EmbeddingClient client) {
    public async Task<int> EmbedAsync(int limit, int batchSize, CancellationToken cancellationToken) {
        if (limit <= 0) {
            throw new ArgumentOutOfRangeException(nameof(limit), "limit must be positive");
        }

        if (batchSize <= 0) {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "batch-size must be positive");
        }

        var config = RimdexConfig.Load();
        var pending = repository.ReadPendingEmbeddingRows(limit, config.Model);
        if (pending.Count == 0) {
            Console.WriteLine("no pending mods to embed");
            return 0;
        }

        var embedded = 0;
        foreach (var batch in pending.Chunk(batchSize)) {
            var vectors = await client.FetchAsync([.. batch.Select(row => row.SearchText)], config, cancellationToken);
            var embeddings = vectors.Select((t, i) => new ModEmbedding(
                batch[i].ModId,
                batch[i].SearchTextHash,
                EmbeddingVector.ToBlob(t),
                t.Length)).ToArray();

            repository.UpsertEmbeddings(config.Model, embeddings);
            embedded += batch.Length;
            Console.WriteLine($"embedded {embedded}/{pending.Count}");
        }

        return 0;
    }

    public static EmbeddingService Create() =>
        new(new ModRepository(AppPaths.DatabasePath), new EmbeddingClient(new HttpClient()));
}