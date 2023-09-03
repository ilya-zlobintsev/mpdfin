using System.Reflection;
using Tmds.DBus;

namespace Mpdfin.MediaKeys.Linux;

class Player : IPlayer
{
    static readonly ObjectPath Path = new("/org/mpris/MediaPlayer2");
    readonly PlayerProperties properties;
    readonly Dictionary<string, PropertyInfo> propertiesProperties;
    public event Action<PropertyChanges>? OnPropertiesChanged;
    private readonly Mpdfin.Player.Player _player;

    public ObjectPath ObjectPath => Path;

    public Player(Mpdfin.Player.Player Player)
    {
        properties = new(Player);
        propertiesProperties = new();
        _player = Player;

        foreach (var property in properties.GetType().GetProperties())
        {
            propertiesProperties.Add(property.Name, property);
        }
    }

    public Task<PlayerProperties> GetAllAsync()
    {
        return Task.FromResult(properties);
    }

    public Task<object> GetAsync(string prop)
    {
        var field = propertiesProperties[prop];
        var value = field.GetValue(properties) ?? throw new Exception("Property not found");
        return Task.FromResult(value);
    }

    public Task NextAsync()
    {
        _player.NextSong();
        return Task.CompletedTask;
    }

    public Task PauseAsync()
    {
        _player.SetPause(true);
        return Task.CompletedTask;
    }

    public Task PlayAsync()
    {
        _player.PlayCurrent();
        return Task.CompletedTask;
    }

    public Task PlayPauseAsync()
    {
        _player.SetPause();
        return Task.CompletedTask;
    }

    public Task PreviousAsync()
    {
        _player.PreviousSong();
        return Task.CompletedTask;
    }

    public Task SeekAsync(long Offset)
    {
        throw new NotImplementedException();
    }

    public Task SetAsync(string prop, object val)
    {
        var property = propertiesProperties[prop];
        property.SetValue(properties, val);
        return Task.CompletedTask;
    }

    public Task SetPositionAsync(ObjectPath TrackId, long Position)
    {
        throw new NotImplementedException();
    }

    public Task StopAsync()
    {
        _player.Stop();
        return Task.CompletedTask;
    }

    public Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler)
    {
        return SignalWatcher.AddAsync(this, nameof(OnPropertiesChanged), handler);
    }

    public Task<IDisposable> WatchSeekedAsync(Action<long> handler, Action<Exception>? onError = null)
    {
        throw new NotImplementedException();
    }
}