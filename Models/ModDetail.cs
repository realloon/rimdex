using System.Security.Cryptography;
using System.Text;

namespace Rimdex.Models;

internal sealed record ModDetail(
    string PublishedFileId,
    string Title,
    string Description,
    string PreviewUrl,
    long Subscriptions,
    long Views,
    long TimeUpdated,
    bool IsTranslation) {
    public string SearchText => $"Title: {Title}\nDescription: {Description}";
    public string SearchTextHash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SearchText)));
}