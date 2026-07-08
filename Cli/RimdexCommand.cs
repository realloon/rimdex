using Rimdex.Data;
using Rimdex.Importing;
using Rimdex.Platform;

namespace Rimdex.Cli;

internal static class RimdexCommand {
    public static async Task<int> RunAsync(string[] args) {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help") {
            PrintUsage();
            return 0;
        }

        try {
            return args[0] switch {
                "import" => await ImportAsync(args[1..]),
                "stats" => PrintStats(),
                _ => throw new ArgumentException($"Unknown command: {args[0]}")
            };
        } catch (Exception ex) {
            await Console.Error.WriteLineAsync($"error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ImportAsync(string[] args) {
        if (args.Length != 1) {
            throw new ArgumentException("Usage: rimdex import <details-dir>");
        }

        var dbPath = AppPaths.DatabasePath;
        var repository = new ModRepository(dbPath);
        var importer = new ModImporter(repository);
        var count = await importer.ImportAsync(args[0]);

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

    private static void PrintUsage() {
        Console.WriteLine("""
                          rimdex

                          Usage:
                            rimdex import <details-dir>
                            rimdex stats
                          """);
    }
}