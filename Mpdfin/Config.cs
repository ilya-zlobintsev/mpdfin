using System.Text.Json;

namespace Mpdfin;

record Config(JellyfinConfig Jellyfin, string? LogLevel, int? Port)
{
    public static Config Load()
    {
        var configDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configFilePath = Path.Join(configDir, "mpdfin", "config.json");

        var contents = File.ReadAllBytes(configFilePath);
        return JsonSerializer.Deserialize(contents, SerializerContext.Default.Config)!;
    }
}

record JellyfinConfig(string ServerUrl, string Username, string Password);