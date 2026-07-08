namespace Rimdex.Embedding;

internal sealed record EmbeddingResponse(EmbeddingResponseItem[]? Data);

internal sealed record EmbeddingResponseItem(int? Index, float[]? Embedding);