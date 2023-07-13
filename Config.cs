using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mpdfin;

[JsonSourceGenerationOptions(WriteIndented = true, IncludeFields = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Config))]
[JsonSerializable(typeof(JellyfinConfig))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}

record Config
{
    public required JellyfinConfig Jellyfin;

    public static Config Load()
    {
        var configDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configFilePath = Path.Join(configDir, "mpdfin", "config.json");

        var contents = File.ReadAllText(configFilePath);
        return JsonSerializer.Deserialize(contents, SourceGenerationContext.Default.Config)!;
    }
}

record JellyfinConfig
{
    public required string ServerUrl;
    public required string Username;
    public required string Password;
}