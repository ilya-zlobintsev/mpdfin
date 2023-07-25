using LibVLCSharp.Shared;
using Microsoft.VisualBasic;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    Response Status()
    {
        Response response = new();

        response.Add("repeat"u8, "0"u8);
        response.Add("random"u8, "0"u8);
        response.Add("single"u8, "0"u8);
        response.Add("consume"u8, "0"u8);

        if (Player.CurrentPos is not null)
        {
            response.Add("song"u8, Player.CurrentPos.Value.ToU8String());
        }

        response.Add("volume"u8, Player.Volume.ToU8String());
        response.Add("state"u8, Player.State switch
        {
            VLCState.Playing => "play"u8,
            VLCState.Paused => "pause"u8,
            _ => "stop"u8,
        });
        response.Add("playlist"u8, Player.PlaylistVersion.ToU8String());
        response.Add("playlistlength"u8, Player.Queue.Count.ToU8String());
        response.Add("elapsed"u8, Player.Elapsed.ToU8String());
        response.Add("time"u8, $"{(int)Player.Elapsed}:{Player.Duration}");
        response.Add("duration"u8, Player.Duration.ToU8String());

        return response;
    }

    Response CurrentSong()
    {
        var currentSong = Player.CurrentSong;
        if (currentSong is not null)
        {
            return currentSong.Value.GetResponse(Player.CurrentPos);
        }
        else
        {
            return new();
        }
    }

    async Task<Response> Idle(ClientStream stream, List<string> args)
    {
        using CancellationTokenSource source = new();

        Subsystem[] subsystems;

        if (args.Count == 0)
        {
            subsystems = Enum.GetValues<Subsystem>();
        }
        else
        {
            subsystems = args.Select(arg => Enum.Parse<Subsystem>(arg, true)).ToArray();
        }

        var notificationTask = NotificationsReceiver.WaitForEvent(subsystems);
        var incomingCommandTask = stream.ReadRequest(source.Token);

        var finishedTask = await Task.WhenAny(notificationTask, incomingCommandTask);
        if (finishedTask == notificationTask)
        {
            await source.CancelAsync();
            Response response = new();

            foreach (var subsystem in notificationTask.Result)
            {
                response.Add("changed"u8, Enum.GetName(subsystem));
            }

            return response;
        }
        else
        {
            return new();
        }
    }
}
