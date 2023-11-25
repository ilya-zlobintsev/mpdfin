using System.Text.Json.Serialization;
using Mpdfin.DB;
using Mpdfin.Player;

namespace Mpdfin;

[JsonSerializable(typeof(Config))]
[JsonSerializable(typeof(PlayerState))]
[JsonSerializable(typeof(DatabaseStorage))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true)]
partial class SerializerContext : JsonSerializerContext;