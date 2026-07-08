using System.Text.Json;
using Rimdex.Platform;
using Rimdex.Serialization;

namespace Rimdex.Configuration;

internal sealed record RimdexConfig(EmbeddingConfig Embedding) {
    public static RimdexConfig Load() {
        var path = AppPaths.ConfigPath;
        if (!File.Exists(path)) {
            throw new FileNotFoundException($"Missing config file: {path}");
        }

        using var stream = File.OpenRead(path);
        var config = JsonSerializer.Deserialize(stream, RimdexJsonContext.Default.RimdexConfig)
                     ?? throw new InvalidDataException($"Invalid config file: {path}");

        config.Validate(path);
        return config;
    }

    private void Validate(string path) {
        if (Embedding is null) {
            throw new InvalidDataException($"Missing embedding config in {path}");
        }

        Embedding.Validate(path);
    }
}