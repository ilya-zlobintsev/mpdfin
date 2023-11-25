using System.Text.Json.Serialization;
using Mpdfin.DB;

namespace Mpdfin;

[JsonSerializable(typeof(Config))]
[JsonSerializable(typeof(DatabaseStorage))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true)]
partial class SerializerContext : JsonSerializerContext;