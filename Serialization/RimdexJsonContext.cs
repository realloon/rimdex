using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rimdex.Configuration;
using Rimdex.Embedding;
using Rimdex.Search;

namespace Rimdex.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(EmbeddingRequest))]
[JsonSerializable(typeof(EmbeddingResponse))]
[JsonSerializable(typeof(RimdexConfig))]
[JsonSerializable(typeof(SearchResultDto[]))]
internal sealed partial class RimdexJsonContext : JsonSerializerContext {
    public static RimdexJsonContext Indented { get; } = new(new JsonSerializerOptions {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    });
}