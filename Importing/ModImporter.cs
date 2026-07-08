using Rimdex.Models;

namespace Rimdex.Importing;

internal sealed class ModImporter(Data.ModRepository repository) {
    public async Task<int> ImportAsync(string detailsDir) {
        if (!Directory.Exists(detailsDir)) {
            throw new DirectoryNotFoundException($"Details directory not found: {detailsDir}");
        }

        var paths = Directory
            .EnumerateFiles(detailsDir, "*.json", SearchOption.TopDirectoryOnly)
            .Order(ModFileNameComparer.Instance)
            .ToArray();

        if (paths.Length == 0) {
            throw new InvalidOperationException($"No detail JSON files found in {detailsDir}");
        }

        var mods = new List<ModDetail>(paths.Length);

        foreach (var path in paths) {
            var rawJson = await File.ReadAllTextAsync(path);
            mods.Add(ModDetailParser.Parse(rawJson, path));
        }

        repository.Import(mods);
        return mods.Count;
    }
}