using System.Threading.Channels;
using Serilog;

namespace Mpdfin.Mpd;

readonly struct ClientNotificationsReceiver
{
    static readonly Subsystem[] AllSubsystems = Enum.GetValues<Subsystem>();

    readonly Dictionary<Subsystem, Channel<Subsystem>> Listeners;

    public ClientNotificationsReceiver()
    {
        var options = new BoundedChannelOptions(1)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest,
        };

        Listeners = Enum
            .GetValues<Subsystem>()
            .ToDictionary(s => s, _ => Channel.CreateBounded<Subsystem>(options));
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

        var listeners = Listeners;
        var subsystemEvents = new List<Subsystem>(subsystems.Length);

        foreach (var subsystem in subsystems)
        {
            var listener = listeners[subsystem];
            if (listener.Reader.TryRead(out Subsystem item))
            {
                subsystemEvents.Add(item);
            }
        }

        if (subsystemEvents.Count > 0)
        {
            return subsystemEvents;
        }

        var tasks = subsystems.Select(async Task<Subsystem?> (s) =>
        {
            try
            {
                return await listeners[s].Reader.ReadAsync(ct);
            }
            catch (Exception)
            {
                return default;
            }
        });

        var finishedTask = await Task.WhenAny(tasks);
        if (finishedTask.Result.HasValue)
            subsystemEvents.Add(finishedTask.Result.Value);

        return subsystemEvents;
    }
}
