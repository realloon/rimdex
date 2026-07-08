namespace Rimdex.Search;

internal sealed record SearchResultDto(
    string PublishedFileId,
    string Title,
    string Summary,
    string PreviewUrl,
    long Subscriptions,
    long Views,
    float Distance,
    double RankScore);