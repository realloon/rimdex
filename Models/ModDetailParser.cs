using System.Text.Json;
using Rimdex.Serialization;

namespace Rimdex.Models;

internal static class ModDetailParser {
    public static ModDetail Parse(string rawJson, string path) {
        using var document = JsonDocument.Parse(rawJson);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object) {
            throw new InvalidDataException($"Invalid detail JSON: {path}");
        }

        var tags = ReadTags(root, path);

        return new ModDetail(
            ReadString(root, "publishedfileid", path),
            ReadString(root, "title", path),
            ReadString(root, "description", path),
            JsonSerializer.Serialize(tags, RimdexJsonContext.Default.StringArray),
            ReadString(root, "preview_url", path),
            ReadInt64(root, "subscriptions", path),
            ReadInt64(root, "favorited", path),
            ReadInt64(root, "views", path),
            ReadInt64(root, "time_created", path),
            ReadInt64(root, "time_updated", path),
            ReadString(root, "crawled_at", path),
            rawJson);
    }

    private static string[] ReadTags(JsonElement root, string path) {
        if (!root.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array) {
            throw new InvalidDataException($"Invalid tags in {path}");
        }

        var values = new List<string>();
        var index = 0;

        foreach (var tag in tags.EnumerateArray()) {
            if (tag.ValueKind != JsonValueKind.Object ||
                !tag.TryGetProperty("tag", out var value) ||
                value.ValueKind != JsonValueKind.String) {
                throw new InvalidDataException($"Invalid tags[{index}] in {path}");
            }

            values.Add(value.GetString()!);
            index += 1;
        }

        return values.ToArray();
    }

    private static string ReadString(JsonElement root, string key, string path) {
        if (!root.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String) {
            throw new InvalidDataException($"Invalid {key} in {path}");
        }

        return value.GetString()!;
    }

    private static long ReadInt64(JsonElement root, string key, string path) {
        if (!root.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out var number)) {
            throw new InvalidDataException($"Invalid {key} in {path}");
        }

        return number;
    }
}