using System.Text.Json.Serialization;
using Mpdfin.DB;
using Mpdfin.Player;

namespace Mpdfin;

[JsonSerializable(typeof(Config))]
[JsonSerializable(typeof(PlayerState))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
partial class SerializerContext : JsonSerializerContext;

[JsonSerializable(typeof(DatabaseStorage))]
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
partial class DatabaseSerializerContext : JsonSerializerContext;