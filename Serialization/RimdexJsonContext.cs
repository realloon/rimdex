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
[JsonSerializable(typeof(string[]))]
internal sealed partial class RimdexJsonContext : JsonSerializerContext;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(SearchResultDto[]))]
internal sealed partial class RimdexIndentedJsonContext : JsonSerializerContext;