using System.Threading.Channels;
using Jellyfin.Sdk;
using Serilog;

namespace Mpdfin.Mpd;

readonly struct ClientNotificationsReceiver
{
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
        Log.Debug($"Got event `{subsystem}` on client");
        if (!Listeners[subsystem].Writer.TryWrite(subsystem))
        {
            Log.Debug("Notifications channel full, discarding event");
        }
    }

    public async Task<List<Subsystem>> WaitForEvent(Subsystem[] subsystems)
    {
        Log.Debug($"Subscribing to events {subsystems}");
        List<Task<Subsystem>> tasks = new();
        using CancellationTokenSource source = new();

        List<Subsystem> earlyResponse = new();

        foreach (var subsystem in subsystems)
        {
            var listener = Listeners[subsystem];
            if (listener.Reader.TryRead(out Subsystem item))
            {
                earlyResponse.Add(item);
            }
        }

        if (earlyResponse.Count > 0)
        {
            return earlyResponse;
        }

        foreach (var subsystem in subsystems)
        {
            var listener = Listeners[subsystem];
            var subsystemTask = listener.Reader.ReadAsync(source.Token);
            tasks.Add(subsystemTask.AsTask());
        }

        var finishedTask = await Task.WhenAny(tasks);
        source.Cancel();

        return new List<Subsystem> { finishedTask.Result };
    }
}
