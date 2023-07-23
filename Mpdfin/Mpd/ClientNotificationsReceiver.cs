using System.Threading.Channels;
using Mpdfin;
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
            var subsystemChannel = Channel.CreateUnbounded<Subsystem>();
            Listeners.Add(subsystem, subsystemChannel);
        }
    }

    public void SendEvent(Subsystem subsystem)
    {
        Log.Debug($"Got event `{subsystem}` on client");
        Listeners[subsystem].Writer.TryWrite(subsystem);
    }

    public async Task<Subsystem> WaitForEvent(Subsystem[] subsystems)
    {
        Log.Debug($"Subscribing to events {subsystems}");
        List<Task<Subsystem>> tasks = new();
        CancellationTokenSource source = new();

        foreach (var subsystem in subsystems)
        {
            var listener = Listeners[subsystem];
            var subsystemTask = listener.Reader.ReadAsync(source.Token);
            tasks.Add(subsystemTask.AsTask());
        }

        var finishedTask = await Task.WhenAny(tasks);
        source.Cancel();

        return await finishedTask;
    }
}