using System.Threading.Channels;
using Jellyfin.Sdk;
using Serilog;

namespace Mpdfin.Mpd;

readonly struct ClientNotificationsReceiver
{
    static readonly Subsystem[] AllSubsystems = Enum.GetValues<Subsystem>();

    readonly Dictionary<Subsystem, Channel<Subsystem>> Listeners;

    public ClientNotificationsReceiver()
    {
        Listeners = new();

        foreach (var subsystem in Enum.GetValues<Subsystem>())
        {
            var subsystemChannel = Channel.CreateBounded<Subsystem>(new BoundedChannelOptions(1)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropOldest,
            });
            Listeners.Add(subsystem, subsystemChannel);
        }
    }

    public void SendEvent(Subsystem subsystem)
    {
        if (!Listeners[subsystem].Writer.TryWrite(subsystem))
        {
            Log.Debug("Notifications channel full, discarding event");
        }
    }

    public async Task<List<Subsystem>> WaitForEvent(Subsystem[]? subsystems, CancellationToken ct)
    {
        subsystems ??= AllSubsystems;

        Log.Debug($"Subscribing to events {subsystems}");

        var listeners = Listeners;
        var subsystemEvents = new List<Subsystem>(subsystems.Length);

        foreach (var subsystem in subsystems)
        {
            var listener = Listeners[subsystem];
            if (listener.Reader.TryRead(out Subsystem item))
            {
                subsystemEvents.Add(item);
            }
        }

        if (subsystemEvents.Count > 0)
        {
            return subsystemEvents;
        }

        var tasks = subsystems.Select(s => listeners[s]
            .Reader
            .ReadAsync(ct)
            .AsTask());

        var finishedTask = await Task.WhenAny(tasks);
        subsystemEvents.Add(finishedTask.Result);

        Log.Debug($"Consumed event `{finishedTask.Result}` on client");

        return subsystemEvents;
    }
}
