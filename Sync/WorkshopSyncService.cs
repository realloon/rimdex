using Rimdex.Data;
using Rimdex.Platform;

namespace Rimdex.Sync;

internal sealed class WorkshopSyncService(ModRepository repository, SteamWorkshopClient client) {
    private const int DetailsBatchSize = 100;
    private const int MaxIncrementalPages = 100;
    private const int StablePageTarget = 3;

    public async Task<int> SyncAsync(SyncOptions options, CancellationToken cancellationToken) {
        options.Validate();

        var watermark = repository.ReadMaxUpdatedTime();
        if (options.Full || watermark == 0) {
            Console.WriteLine("sync mode=full");
            return await SyncFullAsync(options.Limit, cancellationToken);
        }

        Console.WriteLine($"sync mode=incremental watermark={watermark}");
        return await SyncIncrementalAsync(options.Limit, watermark, cancellationToken);
    }

    public static WorkshopSyncService Create() {
        return new WorkshopSyncService(
            new ModRepository(AppPaths.DatabasePath),
            new SteamWorkshopClient(new HttpClient {
                Timeout = TimeSpan.FromSeconds(30)
            }));
    }

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
            Console.WriteLine($"sync:update page={page} fetching ids");
            var browse =
                await client.FetchBrowsePageAsync(page, SteamWorkshopClient.LastUpdatedSort, cancellationToken);
            var ids = limit is null
                ? browse.Ids
                : browse.Ids.Take(limit.Value - synced).ToArray();

            if (ids.Length == 0) {
                return 0;
            }

            Console.WriteLine($"sync:update page={page} fetching details={ids.Length}");
            var details = await client.FetchDetailsAsync(ids, cancellationToken);
            repository.Import(details);
            synced += details.Count;
            var pageMaxUpdated = details.Max(detail => detail.TimeUpdated);
            stablePages = pageMaxUpdated <= watermark ? stablePages + 1 : 0;

            Console.WriteLine(
                $"sync:update page={page} synced={synced} pageMaxUpdated={pageMaxUpdated} stable={stablePages}/{StablePageTarget}");

            if (limit is not null && synced >= limit.Value) {
                return 0;
            }

            if (stablePages >= StablePageTarget) {
                return 0;
            }
        }

        throw new InvalidOperationException(
            $"Incremental sync did not stabilize after {MaxIncrementalPages} pages");
    }

    private async Task SyncDetailsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken) {
        var synced = 0;

        for (var index = 0; index < ids.Count; index += DetailsBatchSize) {
            var batch = ids.Skip(index).Take(DetailsBatchSize).ToArray();
            Console.WriteLine($"details fetching {index + 1}-{index + batch.Length}/{ids.Count}");
            var details = await client.FetchDetailsAsync(batch, cancellationToken);
            repository.Import(details);
            synced += details.Count;
            Console.WriteLine($"synced {synced}/{ids.Count}");
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
            Console.WriteLine($"ids page={page} fetching");
            var (currentPage, i, totalCount, strings) =
                await client.FetchBrowsePageAsync(page, sort, cancellationToken);
            if (currentPage != page) {
                throw new InvalidDataException($"Expected browse page {page}, got {currentPage}");
            }

            totalPages = i;

            foreach (var id in strings) {
                if (seen.Add(id)) {
                    ids.Add(id);
                }

                if (limit is null || ids.Count < limit.Value) continue;

                Console.WriteLine($"ids page={page}/{totalPages} unique={ids.Count}/{totalCount}");
                return ids;
            }

            Console.WriteLine($"ids page={page}/{totalPages} unique={ids.Count}/{totalCount}");
        }

        return ids;
    }
}