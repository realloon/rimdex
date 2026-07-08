using System.Text.Json.Serialization;

namespace Rimdex.Serialization;

[JsonSerializable(typeof(string[]))]
internal sealed partial class RimdexJsonContext : JsonSerializerContext;