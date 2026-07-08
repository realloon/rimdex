namespace Rimdex.Data;

internal sealed record SearchEmbeddingRow(
    string PublishedFileId,
    string Title,
    string Description,
    string PreviewUrl,
    long Subscriptions,
    long Views,
    int Dimension,
    byte[] Embedding);