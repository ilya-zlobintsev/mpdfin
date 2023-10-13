using System.Diagnostics;
using LibVLCSharp.Shared;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    Response Status()
    {
        Response response = new();

        response.Add("repeat"u8, "0"u8);
        response.Add("random"u8, Convert.ToUInt32(Player.Random).ToString());
        response.Add("single"u8, "0"u8);
        response.Add("consume"u8, "0"u8);

        if (Player.QueuePos is not null)
        {
            response.Add("song"u8, Player.QueuePos.Value.ToString());
            response.Add("songid"u8, Player.CurrentSong!.Value.Id.ToString());
        }

        var nextSong = Player.QueueNext;
        if (nextSong is not null)
        {
            var (pos, song) = nextSong.Value;
            response.Add("nextsong"u8, pos.ToString());
            response.Add("nextsongid"u8, song.Id.ToString());
        }

        if (Player.Elapsed is not null)
        {
            response.Add("elapsed"u8, Player.Elapsed.Value.ToString());
            response.Add("time"u8, $"{(int)Math.Floor(Player.Elapsed.Value)}:{Player.Duration}");
            response.Add("duration"u8, Player.Duration!.Value.ToString());
        }

        response.Add("volume"u8, Player.Volume.ToString());
        response.Add("state"u8, Player.PlaybackState switch
        {
            VLCState.Playing => "play"u8,
            VLCState.Paused => "pause"u8,
            _ => "stop"u8,
        });
        response.Add("playlist"u8, Player.PlaylistVersion.ToString());
        response.Add("playlistlength"u8, Player.Queue.Count.ToString());

        if (Updating)
            response.Add("updating_db"u8, UpdateJobId.ToString());

        return response;
    }

    Response CurrentSong()
    {
        var currentSong = Player.CurrentSong;
        if (currentSong is not null)
        {
            return currentSong.Value.GetResponse(Player.QueuePos);
        }
        else
        {
            return new();
        }
    }

    Response Stats()
    {
        Response response = new();

        response.Add("artists"u8, Db.GetUniqueTagValues(Tag.Artist).Count().ToString());
        response.Add("albums"u8, Db.GetUniqueTagValues(Tag.Album).Count().ToString());
        response.Add("songs"u8, Db.Items.Count.ToString());

        return response;
    }

    async Task<Response> Idle(ClientStream stream, List<string> args)
    {
        var subsystems = args.Count > 0
            ? args.Select(arg => Enum.Parse<Subsystem>(arg, true)).ToArray()
            : null;

        using CancellationTokenSource source = new();

        var notificationTask = NotificationsReceiver.WaitForEvent(subsystems, source.Token);
        var incomingCommandTask = stream.ReadRequest(source.Token);

        var finishedTask = await Task.WhenAny(notificationTask, incomingCommandTask);
        source.Cancel();

        if (finishedTask == notificationTask)
        {
            Response response = new();

            foreach (var subsystem in notificationTask.Result)
            {
                response.Add("changed"u8, Enum.GetName(subsystem));
            }

            return response;
        }
        else if (finishedTask == incomingCommandTask)
        {
            if (incomingCommandTask.Result?.Command == Command.noidle || incomingCommandTask.Result is null)
            {
                return new();
            }
            else
            {
                throw new Exception($"Only `noidle` can be called when idling, got `{incomingCommandTask.Result?.Command}`");
            }
        }
        else
        {
            throw new UnreachableException();
        }
    }
}
