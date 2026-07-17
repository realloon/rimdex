using System.Text.Json;
using System.Text.RegularExpressions;
using Rimdex.Models;
using Rimdex.Serialization;

namespace Rimdex.Sync;

internal sealed partial class SteamWorkshopClient(HttpClient httpClient) {
    private const int AppId = 294100;
    private const string RequiredTag = "1.6";
    public const string PopularSort = "totaluniquesubscribers";
    public const string LastUpdatedSort = "lastupdated";

    public async Task<WorkshopBrowsePage> FetchBrowsePageAsync(
        int page,
        string sort,
        CancellationToken cancellationToken) {
        var url = new UriBuilder("https://steamcommunity.com/workshop/browse/") {
            Query = BuildQuery(new Dictionary<string, string> {
                ["appid"] = AppId.ToString(),
                ["browsesort"] = sort,
                ["section"] = "readytouseitems",
                ["p"] = page.ToString(),
                ["requiredtags[]"] = RequiredTag
            })
        }.Uri;

        using var response = await FetchWithRetryAsync(
            () => httpClient.GetAsync(url, cancellationToken),
            $"browse page {page}");
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var ids = ItemLinkPattern()
            .Matches(html)
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .ToArray();

        if (ids.Length == 0) {
            throw new InvalidDataException($"Steam browse page {page} returned no item ids");
        }

        return new WorkshopBrowsePage(
            ExtractNumber(html, "current_page"),
            ExtractNumber(html, "total_pages"),
            ExtractNumber(html, "total_count"),
            ids);
    }

    public async Task<IReadOnlyList<ModDetail>> FetchDetailsAsync(
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken) {
        using var response = await FetchWithRetryAsync(
            () => {
                var content = new FormUrlEncodedContent(BuildDetailsForm(ids));
                return httpClient.PostAsync(
                    "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
                    content,
                    cancellationToken);
            },
            $"details batch {ids[0]}..{ids[^1]}");
        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(rawJson);
        var details = document.RootElement
            .GetProperty("response")
            .GetProperty("publishedfiledetails");

        if (details.ValueKind != JsonValueKind.Array) {
            throw new InvalidDataException("Steam details response is missing publishedfiledetails");
        }

        var crawledAt = DateTimeOffset.UtcNow.ToString("O");
        var byId = new Dictionary<string, ModDetail>();
        foreach (var detail in details.EnumerateArray()) {
            var mod = ReadModDetail(detail, crawledAt);
            byId.Add(mod.PublishedFileId, mod);
        }

        if (byId.Count != ids.Count) {
            throw new InvalidDataException(
                $"Steam details response returned {byId.Count} items for {ids.Count} ids");
        }

        return ids.Select(id => byId[id]).ToArray();
    }

    private static Dictionary<string, string> BuildDetailsForm(IReadOnlyList<string> ids) {
        var values = new Dictionary<string, string> {
            ["itemcount"] = ids.Count.ToString()
        };

        for (var index = 0; index < ids.Count; index++) {
            values[$"publishedfileids[{index}]"] = ids[index];
        }

        return values;
    }

    private static string BuildQuery(IReadOnlyDictionary<string, string> values) {
        return string.Join("&", values.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    private static ModDetail ReadModDetail(JsonElement detail, string crawledAt) {
        var tags = detail.GetProperty("tags")
            .EnumerateArray()
            .Select(tag => tag.GetProperty("tag").GetString()
                           ?? throw new InvalidDataException("Steam detail tag is missing tag"))
            .ToArray();

        return new ModDetail(
            ReadString(detail, "publishedfileid"),
            ReadString(detail, "title"),
            ReadString(detail, "description"),
            JsonSerializer.Serialize(tags, RimdexJsonContext.Default.StringArray),
            ReadString(detail, "preview_url"),
            ReadInt64(detail, "subscriptions"),
            ReadInt64(detail, "favorited"),
            ReadInt64(detail, "views"),
            ReadInt64(detail, "time_created"),
            ReadInt64(detail, "time_updated"),
            crawledAt,
            detail.GetRawText());
    }

    private static string ReadString(JsonElement element, string name) {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) {
            throw new InvalidDataException($"Steam detail is missing string field: {name}");
        }

        return value.GetString()!;
    }

    private static long ReadInt64(JsonElement element, string name) {
        if (!element.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out var number)) {
            throw new InvalidDataException($"Steam detail is missing integer field: {name}");
        }

        return number;
    }

    private static async Task<HttpResponseMessage> FetchWithRetryAsync(
        Func<Task<HttpResponseMessage>> fetch,
        string context) {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= 3; attempt++) {
            try {
                var response = await fetch();
                if (!response.IsSuccessStatusCode) {
                    var status = $"{(int)response.StatusCode} {response.ReasonPhrase}";
                    response.Dispose();
                    throw new HttpRequestException(
                        $"{context} failed: {status}");
                }

                return response;
            } catch (Exception ex) when (attempt < 3 && ex is not OperationCanceledException) {
                lastError = ex;
                Console.WriteLine($"{context} failed attempt={attempt}/3; retrying");
                await Task.Delay(TimeSpan.FromSeconds(attempt));
            }
        }

        throw lastError ?? new HttpRequestException($"{context} failed");
    }

    private static int ExtractNumber(string html, string key) {
        var start = html.IndexOf(key, StringComparison.Ordinal);
        if (start < 0) {
            throw new InvalidDataException($"Steam browse page is missing {key}");
        }

        var match = NumberPattern().Match(html[start..]);
        if (!match.Success) {
            throw new InvalidDataException($"Steam browse page is missing {key} value");
        }

        return int.Parse(match.Groups[1].Value);
    }

    [GeneratedRegex("aspectratio_16x9\"><a href=\"https://steamcommunity.com/sharedfiles/filedetails/\\?id=(\\d+)")]
    private static partial Regex ItemLinkPattern();

    [GeneratedRegex(":(\\d+)")]
    private static partial Regex NumberPattern();
}

internal sealed record WorkshopBrowsePage(
    int CurrentPage,
    int TotalPages,
    int TotalCount,
    string[] Ids);