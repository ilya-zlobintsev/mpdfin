using System.Text.Json;
using System.Text.Json.Serialization;
using LibVLCSharp.Shared;
using Serilog;

namespace Mpdfin.Player;

[JsonSourceGenerationOptions(IncludeFields = true)]
[JsonSerializable(typeof(PlayerState))]
internal partial class PlayerStateContext : JsonSerializerContext;

public class PlayerState
{
    public int Volume { get; set; }
    public int? CurrentPos { get; set; }
    public int PlaylistVersion { get; set; }
    public VLCState PlaybackState { get; init; }
    public List<QueueItem> QueueItems { get; init; } = [];
    public bool Random { get; init; }
    public int NextSongId { get; init; }
    public double? Elapsed { get; init; }

    public static PlayerState? Load()
    {
        var filePath = FilePath();

        try
        {
            using var file = File.OpenRead(filePath);
            return JsonSerializer.Deserialize(file, PlayerStateContext.Default.PlayerState) ?? new PlayerState();
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
        using var file = File.OpenWrite(filePath);
        await JsonSerializer.SerializeAsync(file, this, PlayerStateContext.Default.PlayerState);
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
