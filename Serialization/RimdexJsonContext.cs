using System.Text.Json.Serialization;
using Rimdex.Configuration;
using Rimdex.Embedding;

namespace Rimdex.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(EmbeddingRequest))]
[JsonSerializable(typeof(EmbeddingResponse))]
[JsonSerializable(typeof(RimdexConfig))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class RimdexJsonContext : JsonSerializerContext;