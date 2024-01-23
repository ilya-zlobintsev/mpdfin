using DistIL.Attributes;
using LibVLCSharp.Shared;
using Serilog;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    Response Status()
    {
        Response response = new();

        response.Add("repeat"u8, "0"u8);
        response.Add("random"u8, Player.Queue.Random ? "1"u8 : "0"u8);
        response.Add("single"u8, "0"u8);
        response.Add("consume"u8, "0"u8);

        if (Player.CurrentPos is not null)
            response.Add("song"u8, Player.CurrentPos.Value);

        if (Player.CurrentSong is not null)
            response.Add("songid"u8, Player.CurrentSong.Id);

        var nextSong = Player.NextSong;
        if (nextSong is not null)
        {
            response.Add("nextsong"u8, Player.NextPos!.Value);
            response.Add("nextsongid"u8, nextSong.SongId);
        }

        if (Player.Elapsed is not null)
        {
            response.Add("elapsed"u8, Player.Elapsed.Value);
            response.Add("time"u8, u8($"{(int)Math.Floor(Player.Elapsed.Value)}:{Player.Duration}"));
            response.Add("duration"u8, Player.Duration!.Value);
        }

        response.Add("volume"u8, Player.Volume);
        response.Add("state"u8, Player.PlaybackState switch
        {
            VLCState.Playing => "play"u8,
            VLCState.Paused => "pause"u8,
            _ => "stop"u8,
        });
        response.Add("playlist"u8, Player.PlaylistVersion);
        response.Add("playlistlength"u8, Player.Queue.Count);

        if (Updating)
            response.Add("updating_db"u8, UpdateJobId);

        return response;
    }

    Response CurrentSong()
    {
        return Player.CurrentSong?.GetResponse(Db) ?? new();
    }

    [Optimize]
    Response Stats()
    {
        Response response = new();

        response.Add("artists"u8, Db.GetUniqueTagValues(Tag.Artist).Count());
        response.Add("albums"u8, Db.GetUniqueTagValues(Tag.Album).Count());
        response.Add("songs"u8, Db.Items.Count);

        return response;
    }

    [Optimize]
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

        try
        {
            if (finishedTask == notificationTask)
            {
                Response response = new();

                foreach (var subsystem in notificationTask.Result)
                {
                    response.Add("changed"u8, u8(Enum.GetName(subsystem)!));
                }

                return response;
            }
            else
            {
                return incomingCommandTask.Result is null or { Command: Command.noidle }
                    ? new() : throw new Exception($"Only `noidle` can be called when idling, got `{incomingCommandTask.Result?.Command}`");
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"XD {ex}");
            return new();
        }
    }
}
