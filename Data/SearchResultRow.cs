namespace Rimdex.Data;

internal sealed record SearchResultRow(
    string PublishedFileId,
    string Title,
    string Description,
    string PreviewUrl,
    long Subscriptions,
    long Views);