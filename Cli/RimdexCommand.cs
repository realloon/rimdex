using System.CommandLine;
using Rimdex.Data;
using Rimdex.Importing;
using Rimdex.Platform;

namespace Rimdex.Cli;

internal static class RimdexCommand {
    public static async Task<int> RunAsync(string[] args) {
        var command = CreateRootCommand();
        var parseResult = command.Parse(args);

        try {
            return await parseResult.InvokeAsync();
        } catch (Exception ex) {
            await Console.Error.WriteLineAsync($"error: {ex.Message}");
            return 1;
        }
    }

    private static RootCommand CreateRootCommand() {
        var root = new RootCommand("RimWorld Workshop mod index and semantic search CLI");
        root.Subcommands.Add(CreateImportCommand());
        root.Subcommands.Add(CreateStatsCommand());
        return root;
    }

    private static Command CreateImportCommand() {
        var detailsDir = new Argument<string>("details-dir") {
            Description = "Directory containing crawled Workshop detail JSON files."
        };

        var command = new Command("import", "Import crawled mod details into SQLite.");
        command.Arguments.Add(detailsDir);
        command.SetAction(parseResult => ImportAsync(parseResult.GetRequiredValue(detailsDir)));
        return command;
    }

    private static Command CreateStatsCommand() {
        var command = new Command("stats", "Print database stats.");
        command.SetAction(_ => PrintStats());
        return command;
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