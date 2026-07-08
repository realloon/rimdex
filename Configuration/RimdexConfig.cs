using System.Text.Json;
using Rimdex.Platform;
using Rimdex.Serialization;

namespace Rimdex.Configuration;

internal sealed record RimdexConfig(string ApiKey, string BaseUrl, string Model) {
    public Uri BaseUri {
        get {
            var normalized = BaseUrl.EndsWith('/') ? BaseUrl : $"{BaseUrl}/";
            return new Uri(normalized, UriKind.Absolute);
        }
    }

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

    public void Save() {
        var path = AppPaths.ConfigPath;
        Validate(path);

        var directory = Path.GetDirectoryName(path)
                        ?? throw new InvalidOperationException($"Invalid config path: {path}");
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(this, RimdexJsonContext.Default.RimdexConfig);
        File.WriteAllText(path, $"{json}{Environment.NewLine}");
    }

    private void Validate(string path) {
        if (string.IsNullOrWhiteSpace(ApiKey)) {
            throw new InvalidDataException($"Missing apiKey in {path}");
        }

        if (string.IsNullOrWhiteSpace(BaseUrl)) {
            throw new InvalidDataException($"Missing baseUrl in {path}");
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _)) {
            throw new InvalidDataException($"Invalid baseUrl in {path}");
        }

        if (string.IsNullOrWhiteSpace(Model)) {
            throw new InvalidDataException($"Missing model in {path}");
        }
    }
}