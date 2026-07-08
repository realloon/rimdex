using System.Text.Json.Serialization;
using Rimdex.Configuration;

namespace Rimdex.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RimdexConfig))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class RimdexJsonContext : JsonSerializerContext;