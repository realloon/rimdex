namespace Rimdex.Search;

internal sealed record SearchResultDto(
    string Url,
    string Title,
    string Summary,
    string PreviewUrl,
    long Subscriptions,
    long Views);