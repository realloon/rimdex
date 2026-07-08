namespace Rimdex.Data;

internal sealed record SearchCandidateRow(
    int Id,
    string PublishedFileId,
    string Title,
    string Description,
    string PreviewUrl,
    long Subscriptions,
    long Views,
    float Distance);