using System.Text.Json;
using System.Text.RegularExpressions;
using Rimdex.Data;
using Rimdex.Models;
using Rimdex.Platform;
using Rimdex.Serialization;

namespace Rimdex.Sync;

internal sealed record WorkshopBrowsePage(
    int CurrentPage,
    int TotalPages,
    int TotalCount,
    string[] Ids);

internal sealed partial class SteamWorkshopClient(HttpClient httpClient) {
    private const int AppId = 294100;
    private const string RequiredTag = "1.6";
    public const string PopularSort = "totaluniquesubscribers";
    public const string LastUpdatedSort = "lastupdated";

    public async Task<WorkshopBrowsePage> FetchBrowsePageAsync(
        int page,
        string sort,
        CancellationToken cancellationToken) {
        var url =
            $"https://steamcommunity.com/workshop/browse/?appid={AppId}&browsesort={sort}&section=readytouseitems&p={page}&requiredtags%5B%5D={RequiredTag}";

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
                var form = new List<KeyValuePair<string, string>>(ids.Count + 1) {
                    new("itemcount", ids.Count.ToString())
                };
                form.AddRange(ids.Select((t, i) => new KeyValuePair<string, string>($"publishedfileids[{i}]", t)));

                return httpClient.PostAsync(
                    "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
                    new FormUrlEncodedContent(form),
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
        var byId = details.EnumerateArray()
            .Select(detail => ReadModDetail(detail, crawledAt))
            .ToDictionary(mod => mod.PublishedFileId);

        return byId.Count == ids.Count
            ? ids.Select(id => byId[id]).ToArray()
            : throw new InvalidDataException($"Steam details response returned {byId.Count} items for {ids.Count} ids");
    }

    private static ModDetail ReadModDetail(JsonElement detail, string crawledAt) {
        var tags = detail.GetProperty("tags").EnumerateArray()
            .Select(tag => tag.GetProperty("tag").GetString()!)
            .ToArray();

        return new ModDetail(
            detail.GetProperty("publishedfileid").GetString()!,
            detail.GetProperty("title").GetString()!,
            detail.GetProperty("description").GetString()!,
            JsonSerializer.Serialize(tags, RimdexJsonContext.Default.StringArray),
            detail.GetProperty("preview_url").GetString()!,
            detail.GetProperty("subscriptions").GetInt64(),
            detail.GetProperty("favorited").GetInt64(),
            detail.GetProperty("views").GetInt64(),
            detail.GetProperty("time_created").GetInt64(),
            detail.GetProperty("time_updated").GetInt64(),
            crawledAt,
            detail.GetRawText());
    }

    private static async Task<HttpResponseMessage> FetchWithRetryAsync(
        Func<Task<HttpResponseMessage>> fetch,
        string context) {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= 3; attempt++) {
            try {
                var response = await fetch();
                if (response.IsSuccessStatusCode) {
                    return response;
                }

                var status = $"{(int)response.StatusCode} {response.ReasonPhrase}";
                response.Dispose();
                throw new HttpRequestException($"{context} failed: {status}");
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

        return match.Success
            ? int.Parse(match.Groups[1].Value)
            : throw new InvalidDataException($"Steam browse page is missing {key} value");
    }

    [GeneratedRegex("aspectratio_16x9\"><a href=\"https://steamcommunity.com/sharedfiles/filedetails/\\?id=(\\d+)")]
    private static partial Regex ItemLinkPattern();

    [GeneratedRegex(":(\\d+)")]
    private static partial Regex NumberPattern();
}

internal sealed class WorkshopSyncService(ModRepository repository, SteamWorkshopClient client) {
    private const int DetailsBatchSize = 100;
    private const int MaxIncrementalPages = 100;
    private const int StablePageTarget = 3;

    public async Task<int> SyncAsync(int? limit, bool full, CancellationToken cancellationToken) {
        if (limit is <= 0) {
            throw new ArgumentOutOfRangeException(nameof(limit), "limit must be positive");
        }

        var watermark = repository.ReadMaxUpdatedTime();
        if (full || watermark == 0) {
            Console.WriteLine("sync mode=full");
            return await SyncFullAsync(limit, cancellationToken);
        }

        Console.WriteLine($"sync mode=incremental watermark={watermark}");
        return await SyncIncrementalAsync(limit, watermark, cancellationToken);
    }

    public static WorkshopSyncService Create() =>
        new(new ModRepository(AppPaths.DatabasePath),
            new SteamWorkshopClient(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }));

    private async Task<int> SyncFullAsync(int? limit, CancellationToken cancellationToken) {
        var ids = await FetchIdsAsync(limit, SteamWorkshopClient.PopularSort, cancellationToken);
        await SyncDetailsAsync(ids, cancellationToken);
        return 0;
    }

    private async Task<int> SyncIncrementalAsync(
        int? limit,
        long watermark,
        CancellationToken cancellationToken) {
        var synced = 0;
        var stablePages = 0;

        for (var page = 1; page <= MaxIncrementalPages; page++) {
            var browse =
                await client.FetchBrowsePageAsync(page, SteamWorkshopClient.LastUpdatedSort, cancellationToken);
            var ids = limit is null
                ? browse.Ids
                : browse.Ids.Take(limit.Value - synced).ToArray();

            if (ids.Length == 0) {
                return 0;
            }

            var details = await client.FetchDetailsAsync(ids, cancellationToken);
            repository.Import(details);
            synced += details.Count;
            var pageMaxUpdated = details.Max(detail => detail.TimeUpdated);
            stablePages = pageMaxUpdated <= watermark ? stablePages + 1 : 0;

            Console.WriteLine(
                $"sync:update page={page} synced={synced} pageMaxUpdated={pageMaxUpdated} stable={stablePages}/{StablePageTarget}");

            if (synced >= limit || stablePages >= StablePageTarget) {
                return 0;
            }
        }

        throw new InvalidOperationException($"Incremental sync did not stabilize after {MaxIncrementalPages} pages");
    }

    private async Task SyncDetailsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken) {
        var synced = 0;

        for (var index = 0; index < ids.Count; index += DetailsBatchSize) {
            var batch = ids.Skip(index).Take(DetailsBatchSize).ToArray();
            var details = await client.FetchDetailsAsync(batch, cancellationToken);
            repository.Import(details);
            synced += details.Count;
            if (synced % 1000 == 0 || synced == ids.Count) {
                Console.WriteLine($"synced {synced}/{ids.Count}");
            }
        }
    }

    private async Task<IReadOnlyList<string>> FetchIdsAsync(
        int? limit,
        string sort,
        CancellationToken cancellationToken) {
        var ids = new List<string>();
        var seen = new HashSet<string>();
        var totalPages = 0;

        for (var page = 1; totalPages == 0 || page <= totalPages; page++) {
            var browse = await client.FetchBrowsePageAsync(page, sort, cancellationToken);
            if (browse.CurrentPage != page) {
                throw new InvalidDataException($"Expected browse page {page}, got {browse.CurrentPage}");
            }

            totalPages = browse.TotalPages;

            foreach (var id in browse.Ids) {
                if (seen.Add(id)) {
                    ids.Add(id);
                }

                if (limit is null || ids.Count < limit.Value) continue;

                ReportIdsProgress(page, totalPages, ids.Count, browse.TotalCount);
                return ids;
            }

            if (ShouldReportPage(page, totalPages)) {
                ReportIdsProgress(page, totalPages, ids.Count, browse.TotalCount);
            }
        }

        return ids;
    }

    private static bool ShouldReportPage(int page, int totalPages) => page == 1 || page % 10 == 0 || page == totalPages;

    private static void ReportIdsProgress(int page, int totalPages, int unique, int totalCount) {
        Console.WriteLine($"ids page={page}/{totalPages} unique={unique}/{totalCount}");
    }
}