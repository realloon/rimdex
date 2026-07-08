using System.CommandLine;
using Rimdex.Configuration;
using Rimdex.Data;
using Rimdex.Importing;
using Rimdex.Platform;

namespace Rimdex.Cli;

internal static class RimdexCommand {
    public static async Task<int> RunAsync(string[] args) {
        var command = CreateRootCommand();
        var parseResult = command.Parse(args);
        return await parseResult.InvokeAsync();
    }

    private static RootCommand CreateRootCommand() {
        var root = new RootCommand("RimWorld Workshop mod index and semantic search CLI");
        root.Subcommands.Add(CreateConfigCommand());
        root.Subcommands.Add(CreateImportCommand());
        root.Subcommands.Add(CreateStatsCommand());
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

        var command = new Command("set", "Write embedding configuration.");
        command.Options.Add(apiKey);
        command.Options.Add(baseUrl);
        command.Options.Add(model);
        command.SetAction(parseResult => Run(() => SaveConfig(
            parseResult.GetRequiredValue(apiKey),
            parseResult.GetRequiredValue(baseUrl),
            parseResult.GetRequiredValue(model))));
        return command;
    }

    private static Command CreateImportCommand() {
        var detailsDir = new Argument<string>("details-dir") {
            Description = "Directory containing crawled Workshop detail JSON files."
        };

        var command = new Command("import", "Import crawled mod details into SQLite.");
        command.Arguments.Add(detailsDir);
        command.SetAction(parseResult => RunAsync(() => ImportAsync(parseResult.GetRequiredValue(detailsDir))));
        return command;
    }

    private static Command CreateStatsCommand() {
        var command = new Command("stats", "Print database stats.");
        command.SetAction(_ => Run(PrintStats));
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

    private static async Task<int> ImportAsync(string detailsDir) {
        var dbPath = AppPaths.DatabasePath;
        var repository = new ModRepository(dbPath);
        var importer = new ModImporter(repository);
        var count = await importer.ImportAsync(detailsDir);

        Console.WriteLine($"imported {count} mods into {dbPath}");
        return 0;
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
}