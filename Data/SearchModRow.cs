namespace Rimdex.Data;

internal sealed record SearchModRow(
    int Id,
    string PublishedFileId,
    string Title,
    string Description,
    string PreviewUrl,
    long Subscriptions,
    long Views);