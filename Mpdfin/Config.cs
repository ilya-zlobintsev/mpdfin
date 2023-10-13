using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Mpdfin;

[RequiresUnreferencedCode("Serialization")]
class Config
{
    public required JellyfinConfig Jellyfin { get; set; }
    public string? LogLevel { get; init; }
    public int? Port { get; init; }

    public static string GetPath()
    {
        var configDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Join(configDir, "mpdfin", "config.json");
    }

    public static Config Load()
    {
        var configFilePath = GetPath();
        var contents = File.ReadAllText(configFilePath);
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return JsonSerializer.Deserialize<Config>(contents, options)!;
    }
}

class JellyfinConfig
{
    public required string ServerUrl { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
}
