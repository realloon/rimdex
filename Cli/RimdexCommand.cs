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
        try {
            return await CreateRootCommand().Parse(args).InvokeAsync();
        } catch (Exception ex) {
            await Console.Error.WriteLineAsync($"error: {ex.Message}");
            return 1;
        }
    }

    private static RootCommand CreateRootCommand() =>
        new("Search RimWorld Workshop mods.") {
            CreateConfigCommand(),
            CreateEmbedCommand(),
            CreateSearchCommand(),
            CreateStatsCommand(),
            CreateSyncCommand()
        };

    private static Command CreateConfigCommand() =>
        new("config", "Manage rimdex configuration.") {
            CreateConfigSetCommand()
        };

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

        var command = new Command("set", "Save embedding configuration.") { apiKey, baseUrl, model };
        command.SetAction(parseResult => {
            new RimdexConfig(
                parseResult.GetRequiredValue(apiKey),
                parseResult.GetRequiredValue(baseUrl),
                parseResult.GetRequiredValue(model)).Save();
            Console.WriteLine($"wrote config to {AppPaths.ConfigPath}");
            return 0;
        });
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

        var command = new Command("embed", "Update the semantic search index.") { limit, batchSize };
        command.SetAction((parseResult, ct) => EmbeddingService.Create().EmbedAsync(
            parseResult.GetRequiredValue(limit),
            parseResult.GetRequiredValue(batchSize),
            ct));
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
        var keyword = new Option<bool>("--keyword") {
            Description = "Use keyword search."
        };

        var command = new Command("search", "Search RimWorld mods.") { query, limit, keyword };
        command.SetAction((parseResult, ct) => SearchService.Create().SearchAsync(
            parseResult.GetRequiredValue(query),
            parseResult.GetRequiredValue(limit),
            parseResult.GetValue(keyword),
            ct));
        return command;
    }

    private static Command CreateStatsCommand() {
        var command = new Command("stats", "Show local data stats.");
        command.SetAction(_ => {
            var stats = new ModRepository(AppPaths.DatabasePath).ReadStats();
            Console.WriteLine($"db: {AppPaths.DatabasePath}");
            Console.WriteLine($"mods: {stats.Mods}");
            Console.WriteLine($"embeddings: {stats.Embeddings}");
            return 0;
        });
        return command;
    }

    private static Command CreateSyncCommand() {
        var limit = new Option<int?>("--limit") {
            Description = "Maximum Workshop items to update."
        };
        var full = new Option<bool>("--full") {
            Description = "Refresh the full Workshop index."
        };

        var command = new Command("sync", "Update local data.") { limit, full };
        command.SetAction((parseResult, ct) => WorkshopSyncService.Create().SyncAsync(
            parseResult.GetValue(limit),
            parseResult.GetValue(full),
            ct));
        return command;
    }
}