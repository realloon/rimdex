namespace Rimdex.Models;

internal sealed record ModDetail(
    string PublishedFileId,
    string Title,
    string Description,
    string TagsJson,
    string PreviewUrl,
    long Subscriptions,
    long Favorited,
    long Views,
    long TimeCreated,
    long TimeUpdated,
    string CrawledAt,
    string RawJson) {
    public string SearchText => $"Title: {Title}\nDescription: {Description}";
}