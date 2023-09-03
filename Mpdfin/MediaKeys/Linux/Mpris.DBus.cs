using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using LibVLCSharp.Shared;
using Tmds.DBus;

[assembly: InternalsVisibleTo(Connection.DynamicAssemblyName)]
namespace Mpdfin.MediaKeys.Linux;

[DBusInterface("org.mpris.MediaPlayer2")]
interface IMediaPlayer2 : IDBusObject
{
    Task RaiseAsync();
    Task QuitAsync();
    Task<object> GetAsync(string prop);
    Task<MediaPlayer2Properties> GetAllAsync();
    Task SetAsync(string prop, object val);
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
}

[Dictionary]
class MediaPlayer2Properties
{
    public bool CanQuit;
    public bool CanRaise;
    public bool HasTrackList;
    public string Identity = "Mpdfin";
    public string[] SupportedUriSchemes = Array.Empty<string>();
    public string[] SupportedMimeTypes = Array.Empty<string>();
}

[DBusInterface("org.mpris.MediaPlayer2.Player")]
interface IPlayer : IDBusObject
{
    Task NextAsync();
    Task PreviousAsync();
    Task PauseAsync();
    Task PlayPauseAsync();
    Task StopAsync();
    Task PlayAsync();
    Task SeekAsync(long Offset);
    Task SetPositionAsync(ObjectPath TrackId, long Position);
    Task<IDisposable> WatchSeekedAsync(Action<long> handler, Action<Exception>? onError = null);
    Task<object> GetAsync(string prop);
    Task<PlayerProperties> GetAllAsync();
    Task SetAsync(string prop, object val);
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
}

[Dictionary]
class PlayerProperties
{
    private readonly Mpdfin.Player.Player _player;

    public PlayerProperties(Mpdfin.Player.Player Player)
    {
        _player = Player;
    }

    public string? PlaybackStatus => _player.PlaybackState switch
    {
        VLCState.Playing => "Playing",
        VLCState.Paused => "Paused",
        _ => "Stopped"
    };
    // public string? LoopStatus;
    public static double Rate => 1.0;
    public bool Shuffle
    {
        get => _player.Random;
        set => _player.Random = value;
    }
    public IDictionary<string, object> Metadata = new Dictionary<string, object>();
    public double Volume => _player.Volume;
    public long Position => (long)(_player.Elapsed ?? 0 * 1000 * 1000);
    public static double MinimumRate => 1.0;
    public static double MaximumRate => 1.0;
    public static bool CanGoNext => true;
    public static bool CanGoPrevious => true;
    public static bool CanPlay => true;
    public static bool CanSeek => true;
    public static bool CanControl => true;
}
