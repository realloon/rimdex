using System.CommandLine;
using Rimdex.Configuration;
using Rimdex.Data;
using Rimdex.Embedding;
using Rimdex.Platform;
using Rimdex.Search;
using Rimdex.Sync;

namespace Rimdex.Cli;

internal static class RimdexCommand {
    public static async Task<int> RunAsync(string[] args) {
        var command = CreateRootCommand();
        var parseResult = command.Parse(args);
        return await parseResult.InvokeAsync();
    }

    private static RootCommand CreateRootCommand() {
        var root = new RootCommand("Search RimWorld Workshop mods.");
        root.Subcommands.Add(CreateConfigCommand());
        root.Subcommands.Add(CreateEmbedCommand());
        root.Subcommands.Add(CreateSearchCommand());
        root.Subcommands.Add(CreateStatsCommand());
        root.Subcommands.Add(CreateSyncCommand());
        return root;
    }

    private static Command CreateConfigCommand() {
        var command = new Command("config", "Manage rimdex configuration.");
        command.Subcommands.Add(CreateConfigSetCommand());
        return command;
    }

    private static Command CreateConfigSetCommand() {
        var apiKey = new Option<string>("--api-key") {
            Description = "Embedding API key.",
            Required = true
        };
        var baseUrl = new Option<string>("--base-url") {
            Description = "Embedding API base URL.",
            Required = true
        };
        var model = new Option<string>("--model") {
            Description = "Embedding model name.",
            Required = true
        };

        var command = new Command("set", "Save embedding configuration.");
        command.Options.Add(apiKey);
        command.Options.Add(baseUrl);
        command.Options.Add(model);
        command.SetAction(parseResult => Run(() => SaveConfig(
            parseResult.GetRequiredValue(apiKey),
            parseResult.GetRequiredValue(baseUrl),
            parseResult.GetRequiredValue(model))));
        return command;
    }

    private static Command CreateEmbedCommand() {
        var limit = new Option<int>("--limit") {
            Description = "Maximum mods to index.",
            DefaultValueFactory = _ => 100
        };
        var batchSize = new Option<int>("--batch-size") {
            Description = "Embedding API batch size.",
            DefaultValueFactory = _ => 16
        };

        var command = new Command("embed", "Update the semantic search index.");
        command.Options.Add(limit);
        command.Options.Add(batchSize);
        command.SetAction(parseResult => RunAsync(() => EmbedAsync(new EmbedOptions(
            parseResult.GetRequiredValue(limit),
            parseResult.GetRequiredValue(batchSize)))));
        return command;
    }

    private static Command CreateSearchCommand() {
        var query = new Argument<string>("query") {
            Description = "Search query."
        };
        var limit = new Option<int>("--limit") {
            Description = "Maximum results to return.",
            DefaultValueFactory = _ => 5
        };
        var candidates = new Option<int?>("--candidates") {
            Description = "Semantic candidates to rerank."
        };

        var command = new Command("search", "Search RimWorld mods.");
        command.Arguments.Add(query);
        command.Options.Add(limit);
        command.Options.Add(candidates);
        command.SetAction(parseResult => {
            var resultLimit = parseResult.GetRequiredValue(limit);
            var candidateCount = parseResult.GetValue(candidates) ?? Math.Max(50, resultLimit * 10);

            return RunAsync(() => SearchAsync(new SearchOptions(
                parseResult.GetRequiredValue(query),
                resultLimit,
                candidateCount)));
        });
        return command;
    }

    private static Command CreateStatsCommand() {
        var command = new Command("stats", "Show local data stats.");
        command.SetAction(_ => Run(PrintStats));
        return command;
    }

    private static Command CreateSyncCommand() {
        var limit = new Option<int?>("--limit") {
            Description = "Maximum Workshop items to update."
        };
        var full = new Option<bool>("--full") {
            Description = "Refresh the full Workshop index."
        };

        var command = new Command("sync", "Update local data.");
        command.Options.Add(limit);
        command.Options.Add(full);
        command.SetAction(parseResult => RunAsync(() => SyncAsync(new SyncOptions(
            parseResult.GetValue(limit),
            parseResult.GetValue(full)))));
        return command;
    }

    private static int Run(Func<int> action) {
        try {
            return action();
        } catch (Exception ex) {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunAsync(Func<Task<int>> action) {
        try {
            return await action();
        } catch (Exception ex) {
            await Console.Error.WriteLineAsync($"error: {ex.Message}");
            return 1;
        }
    }

    private static int SaveConfig(string apiKey, string baseUrl, string model) {
        var config = new RimdexConfig(apiKey, baseUrl, model);
        config.Save();
        Console.WriteLine($"wrote config to {AppPaths.ConfigPath}");
        return 0;
    }

    private static async Task<int> EmbedAsync(EmbedOptions options) {
        return await EmbeddingService.Create().EmbedAsync(options, CancellationToken.None);
    }

    private static async Task<int> SearchAsync(SearchOptions options) {
        return await SearchService.Create().SearchAsync(options, CancellationToken.None);
    }

    private static int PrintStats() {
        var dbPath = AppPaths.DatabasePath;
        var repository = new ModRepository(dbPath);
        var stats = repository.ReadStats();

        Console.WriteLine($"db: {dbPath}");
        Console.WriteLine($"mods: {stats.Mods}");
        Console.WriteLine($"embeddings: {stats.Embeddings}");
        return 0;
    }

    private static async Task<int> SyncAsync(SyncOptions options) {
        return await WorkshopSyncService.Create().SyncAsync(options, CancellationToken.None);
    }
}