using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Rimdex.Configuration;
using Rimdex.Serialization;

namespace Rimdex.Embedding;

internal sealed class EmbeddingClient(HttpClient httpClient) {
    public async Task<float[][]> FetchAsync(IReadOnlyList<string> input, RimdexConfig config,
        CancellationToken cancellationToken) {
        if (input.Count == 0) {
            return [];
        }

        var url = new Uri(config.BaseUri, "embeddings");
        var body = JsonSerializer.Serialize(
            new EmbeddingRequest(config.Model, input.ToArray()),
            RimdexJsonContext.Default.EmbeddingRequest);

        using var request = new HttpRequestMessage(HttpMethod.Post, url) {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) {
            throw new HttpRequestException($"Embedding API failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload =
            await JsonSerializer.DeserializeAsync(stream, RimdexJsonContext.Default.EmbeddingResponse,
                cancellationToken)
            ?? throw new InvalidDataException("Embedding API returned an empty response");

        return ParseResponse(payload, input.Count);
    }

    private static float[][] ParseResponse(EmbeddingResponse response, int expectedCount) {
        if (response.Data is null) {
            throw new InvalidDataException("Embedding API response is missing data");
        }

        if (response.Data.Length != expectedCount) {
            throw new InvalidDataException(
                $"Embedding API returned {response.Data.Length} vectors for {expectedCount} inputs");
        }

        var vectors = new float[expectedCount][];
        foreach (var item in response.Data) {
            if (item.Index is null) {
                throw new InvalidDataException("Embedding API response item is missing index");
            }

            var index = item.Index.Value;
            if (index < 0 || index >= expectedCount) {
                throw new InvalidDataException("Embedding API response item has an invalid index");
            }

            if (vectors[index] is not null) {
                throw new InvalidDataException("Embedding API response contains duplicate indexes");
            }

            if (item.Embedding is null || item.Embedding.Length == 0) {
                throw new InvalidDataException("Embedding API response item is missing embedding");
            }

            vectors[index] = item.Embedding;
        }

        if (vectors.Any(vector => vector is null)) {
            throw new InvalidDataException("Embedding API response is missing one or more indexes");
        }

        return vectors!;
    }
}