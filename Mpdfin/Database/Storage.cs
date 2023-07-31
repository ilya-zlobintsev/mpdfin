using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jellyfin.Sdk;
using Serilog;

namespace Mpdfin.DB;

class DatabaseStorage
{
    public List<BaseItemDto> Items { get; set; }
    public AuthenticationResult AuthenticationResult { get; set; }

    public DatabaseStorage(AuthenticationResult auth)
    {
        Items = new();
        AuthenticationResult = auth;
    }

    [JsonConstructor]
    public DatabaseStorage(AuthenticationResult authenticationResult, List<BaseItemDto> items)
    {
        Items = items;
        AuthenticationResult = authenticationResult;
    }

    [RequiresUnreferencedCode("Uses reflection-based serialization")]
    public static DatabaseStorage? Load()
    {
        var filePath = FilePath();

        try
        {
            var contents = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<DatabaseStorage>(contents);
        }
        catch (Exception ex)
        {
            if (ex is not FileNotFoundException && ex is not DirectoryNotFoundException)
                Log.Error($"Could not load local database: {ex}");

            return null;
        }
    }

    [RequiresUnreferencedCode("Uses reflection-based serialization")]
    public async Task Save()
    {
        Directory.CreateDirectory(DataDir());

        var filePath = FilePath();
        var contents = JsonSerializer.Serialize(this);
        await File.WriteAllTextAsync(filePath, contents);
        Log.Information("Saved database file");
    }

    static string DataDir()
    {
        return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mpdfin");
    }

    static string FilePath()
    {
        return Path.Join(DataDir(), "db.json");
    }
}