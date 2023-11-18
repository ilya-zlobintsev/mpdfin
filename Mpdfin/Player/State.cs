using System.Text.Json;
using System.Text.Json.Serialization;
using LibVLCSharp.Shared;
using Serilog;

namespace Mpdfin.Player;

[JsonSourceGenerationOptions(IncludeFields = true)]
[JsonSerializable(typeof(PlayerState))]
internal partial class PlayerStateContext : JsonSerializerContext
{
}

public class PlayerState
{
    public int Volume;
    public int? CurrentPos;
    public int PlaylistVersion;
    public VLCState PlaybackState;
    public List<QueueItem> QueueItems = new();
    public bool Random;
    public int NextSongId;
    public double? Elapsed;

    public static PlayerState? Load()
    {
        var filePath = FilePath();

        try
        {
            var contents = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize(contents, PlayerStateContext.Default.PlayerState) ?? new PlayerState();
        }
        catch (Exception ex)
        {
            if (ex is not FileNotFoundException && ex is not DirectoryNotFoundException)
                Log.Error($"Could not load state: {ex}");
            return null;
        }
    }

    public async Task Save()
    {
        Directory.CreateDirectory(DataDir());

        var filePath = FilePath();
        var contents = JsonSerializer.Serialize(this, PlayerStateContext.Default.PlayerState);
        await File.WriteAllTextAsync(filePath, contents);
        Log.Debug("Saved state");
    }

    static string DataDir()
    {
        return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mpdfin");
    }

    static string FilePath()
    {
        return Path.Join(DataDir(), "state.json");
    }
}
