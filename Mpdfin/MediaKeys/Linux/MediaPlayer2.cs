using System.Reflection;
using Tmds.DBus;

namespace Mpdfin.MediaKeys.Linux;

class MediaPlayer2 : IMediaPlayer2
{
    static readonly ObjectPath Path = new("/org/mpris/MediaPlayer2");
    readonly MediaPlayer2Properties properties;
    readonly Dictionary<string, FieldInfo> propertiesFields;
    public event Action<PropertyChanges>? OnPropertiesChanged;

    public MediaPlayer2()
    {
        properties = new();
        propertiesFields = new();

        foreach (var field in properties.GetType().GetFields())
        {
            propertiesFields.Add(field.Name, field);
        }
    }

    public ObjectPath ObjectPath => Path;

    public Task<MediaPlayer2Properties> GetAllAsync()
    {
        return Task.FromResult(properties);
    }

    public Task<object> GetAsync(string prop)
    {
        var field = propertiesFields[prop];
        var value = field.GetValue(properties) ?? throw new Exception("Property not found");
        return Task.FromResult(value);
    }

    public Task SetAsync(string prop, object val)
    {
        var field = propertiesFields[prop];
        field.SetValue(properties, val);
        OnPropertiesChanged?.Invoke(PropertyChanges.ForProperty(prop, val));
        return Task.CompletedTask;
    }

    public Task QuitAsync()
    {
        return Task.CompletedTask;
    }

    public Task RaiseAsync()
    {
        return Task.CompletedTask;
    }

    public Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler)
    {
        return SignalWatcher.AddAsync(this, nameof(OnPropertiesChanged), handler);
    }
}