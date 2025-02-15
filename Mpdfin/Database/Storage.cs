using System.Text.Json;
using System.Text.Json.Serialization;
using Jellyfin.Sdk.Generated.Models;
using Serilog;

namespace Mpdfin.DB;

public class DatabaseStorage
{
    public List<BaseItemDto> Items { get; set; }
    public AuthenticationResult AuthenticationResult { get; set; }

    public DatabaseStorage(AuthenticationResult auth)
    {
        Items = [];
        AuthenticationResult = auth;
    }

    [JsonConstructor]
    public DatabaseStorage(AuthenticationResult authenticationResult, List<BaseItemDto> items)
    {
        Items = items;
        AuthenticationResult = authenticationResult;
    }

    public static DatabaseStorage? Load()
    {
        var filePath = FilePath();

        try
        {
            using var file = File.OpenRead(filePath);
            return JsonSerializer.Deserialize(
                file, DatabaseSerializerContext.Default.DatabaseStorage);
        }
        catch (Exception ex)
        {
            if (ex is not (FileNotFoundException or DirectoryNotFoundException))
                Log.Error($"Could not load local database: {ex}");

            return null;
        }
    }

    public async Task Save()
    {
        Directory.CreateDirectory(DataDir());

        var filePath = FilePath();
        await using var file = File.OpenWrite(filePath);
        await JsonSerializer.SerializeAsync(
            file, this, DatabaseSerializerContext.Default.DatabaseStorage);

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
