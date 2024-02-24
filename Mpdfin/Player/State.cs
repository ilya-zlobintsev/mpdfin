using System.Text.Json;
using LibVLCSharp.Shared;
using Serilog;

namespace Mpdfin.Player;

public class PlayerState
{
    public int Volume { get; set; }
    public int? CurrentPos { get; set; }
    public int PlaylistVersion { get; set; }
    public VLCState PlaybackState { get; init; }
    public required IReadOnlyList<QueueItem> QueueItems { get; init; }
    public bool Random { get; init; }
    public int NextSongId { get; init; }
    public double? Elapsed { get; init; }

    public static PlayerState? Load()
    {
        var filePath = FilePath();

        try
        {
            using var file = File.OpenRead(filePath);
            return JsonSerializer.Deserialize(file, SerializerContext.Default.PlayerState)
                ?? new PlayerState { QueueItems = [] };
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
        await using var file = File.Create(filePath);
        await JsonSerializer.SerializeAsync(file, this, SerializerContext.Default.PlayerState);
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
