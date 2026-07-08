using Rimdex.Data;
using Rimdex.Importing;

namespace Rimdex.Cli;

internal static class RimdexCommand {
    private const string DefaultDbPath = "data/rimdex.sqlite";

    public static async Task<int> RunAsync(string[] args) {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help") {
            PrintUsage();
            return 0;
        }

        try {
            return args[0] switch {
                "import" => await ImportAsync(args[1..]),
                "stats" => PrintStats(args[1..]),
                _ => throw new ArgumentException($"Unknown command: {args[0]}")
            };
        } catch (Exception ex) {
            await Console.Error.WriteLineAsync($"error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ImportAsync(string[] args) {
        var options = ParseOptions(args);
        if (options.Positionals.Count != 1) {
            throw new ArgumentException("Usage: rimdex import <details-dir> [--db <path>]");
        }

        var repository = new ModRepository(options.DbPath);
        var importer = new ModImporter(repository);
        var count = await importer.ImportAsync(options.Positionals[0]);

        Console.WriteLine($"imported {count} mods into {options.DbPath}");
        return 0;
    }

    private static int PrintStats(string[] args) {
        var options = ParseOptions(args);
        if (options.Positionals.Count != 0) {
            throw new ArgumentException("Usage: rimdex stats [--db <path>]");
        }

        var repository = new ModRepository(options.DbPath);
        var stats = repository.ReadStats();

        Console.WriteLine($"db: {options.DbPath}");
        Console.WriteLine($"mods: {stats.Mods}");
        Console.WriteLine($"embeddings: {stats.Embeddings}");
        return 0;
    }

    private static CommandOptions ParseOptions(string[] args) {
        var options = new CommandOptions(DefaultDbPath);

        for (var i = 0; i < args.Length; i++) {
            if (args[i] == "--db") {
                if (i + 1 >= args.Length) {
                    throw new ArgumentException("Missing value for --db");
                }

                options = options with { DbPath = args[++i] };
                continue;
            }

            if (args[i].StartsWith('-')) {
                throw new ArgumentException($"Unknown option: {args[i]}");
            }

            options.Positionals.Add(args[i]);
        }

        return options;
    }

    private static void PrintUsage() {
        Console.WriteLine("""
                          rimdex

                          Usage:
                            rimdex import <details-dir> [--db <path>]
                            rimdex stats [--db <path>]
                          """);
    }

    private sealed record CommandOptions(string DbPath) {
        public List<string> Positionals { get; } = [];
    }
}