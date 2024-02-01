using DistIL.Attributes;
using LibVLCSharp.Shared;
using Serilog;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    Response Status()
    {
        Response response = new();

        response.Append("repeat"u8, "0"u8);
        response.Append("random"u8, Player.Queue.Random ? "1"u8 : "0"u8);
        response.Append("single"u8, "0"u8);
        response.Append("consume"u8, "0"u8);

        if (Player.CurrentPos is not null)
            response.Append("song"u8, Player.CurrentPos.Value);

        if (Player.CurrentSong is not null)
            response.Append("songid"u8, Player.CurrentSong.Id);

        var nextSong = Player.NextSong;
        if (nextSong is not null)
        {
            response.Append("nextsong"u8, Player.NextPos!.Value);
            response.Append("nextsongid"u8, nextSong.SongId);
        }

        if (Player.Elapsed is not null)
        {
            response.Append("elapsed"u8, Player.Elapsed.Value);
            response.Append("time"u8, u8($"{(int)Math.Floor(Player.Elapsed.Value)}:{Player.Duration}"));
            response.Append("duration"u8, Player.Duration!.Value);
        }

        response.Append("volume"u8, Player.Volume);
        response.Append("state"u8, Player.PlaybackState switch
        {
            VLCState.Playing => "play"u8,
            VLCState.Paused => "pause"u8,
            _ => "stop"u8,
        });
        response.Append("playlist"u8, Player.PlaylistVersion);
        response.Append("playlistlength"u8, Player.Queue.Count);

        if (Updating)
            response.Append("updating_db"u8, UpdateJobId);

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

        response.Append("artists"u8, Db.GetUniqueTagValues(Tag.Artist).Count());
        response.Append("albums"u8, Db.GetUniqueTagValues(Tag.Album).Count());
        response.Append("songs"u8, Db.Items.Count);

        return response;
    }

    [Optimize]
    async Task<Response> Idle(ClientStream stream, List<U8String> args)
    {
        var subsystems = args.Count > 0
            ? args.Select(arg => U8Enum.Parse<Subsystem>(arg, true)).ToArray()
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
                    response.Append("changed"u8, subsystem.ToU8String());
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
