using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog.Events;

namespace Mpdfin;

[JsonSourceGenerationOptions(WriteIndented = true, IncludeFields = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Config))]
[JsonSerializable(typeof(JellyfinConfig))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}

record Config
{
    public required JellyfinConfig Jellyfin { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter<LogEventLevel>))]
    public LogEventLevel? LogLevel;
    public int? Port;

    public static string GetPath()
    {
        var configDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Join(configDir, "mpdfin", "config.json");
    }

    public static Config Load()
    {
        var configFilePath = GetPath();
        var contents = File.ReadAllText(configFilePath);
        return JsonSerializer.Deserialize(contents, SourceGenerationContext.Default.Config)!;
    }
}

record JellyfinConfig
{
    public required string ServerUrl { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
}
