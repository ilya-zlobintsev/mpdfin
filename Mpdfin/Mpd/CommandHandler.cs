using LibVLCSharp.Shared;
using Serilog;

namespace Mpdfin.Mpd;

class CommandHandler
{
    public Player.Player Player;
    public Database Db;

    public CommandHandler(Player.Player player, Database db)
    {
        Player = player;
        Db = db;
    }

    public Response HandleRequest(Request request)
    {
        Log.Debug($"Handling command {request.Command}");
        return request.Command switch
        {
            Command.ping => new(),
            Command.status => Status(),
            Command.currentsong => CurrentSong(),
            Command.play => Play(int.Parse(request.Args[0])),
            Command.playid => PlayId(request.Args[0]),
            Command.pause => Pause(request.Args.ElementAtOrDefault(0)),
            Command.getvol => GetVol(),
            Command.setvol => SetVol(int.Parse(request.Args[0])),
            Command.addid => AddId(request.Args[0]),
            Command.playlistinfo => PlaylistInfo(),
            Command.plchanges => PlChanges(int.Parse(request.Args[0])),
            Command.tagtypes => TagTypes(),
            _ => throw new NotImplementedException($"Command {request.Command} not implemented or cannot be called in the current context"),
        };
    }

    Response Status()
    {
        Response response = new();

        response.Add("volume"u8, Player.Volume.ToU8String());
        response.Add("state"u8, Player.State switch
        {
            VLCState.Playing => "play"u8,
            VLCState.Paused => "pause"u8,
            _ => "stop"u8,
        });
        response.Add("playlist"u8, Player.PlaylistVersion.ToU8String());
        response.Add("playlistlength"u8, Player.Queue.Count.ToU8String());

        return response;
    }

    static Response CurrentSong()
    {
        return new();
    }

    Response AddId(string id)
    {
        var guid = Guid.Parse(id);
        var item = Db.Items.Find(item => item.Id == guid);

        if (item is not null)
        {
            var url = Db.GetAudioStreamUri(item.Id);
            var queueId = Player.Add(url, item);
            return new("Id"u8, queueId.ToString());
        }
        else
        {
            throw new FileNotFoundException($"Item {id} not found");
        }
    }

    Response Play(int pos)
    {
        if (pos >= Player.Queue.Count)
            throw new FileNotFoundException("Invalid song index");

        Player.SetCurrent(pos);

        return new();
    }

    Response PlayId(string id)
    {
        var guid = Guid.Parse(id);
        var index = Player.Queue.FindIndex(song => song.Id == guid);

        if (index == -1)
            throw new FileNotFoundException($"Song with id {id} not found in the database");

        Player.SetCurrent(index);

        return new();
    }

    Response Pause(string? state)
    {
        bool? value = state switch
        {
            "0" => false,
            "1" => true,
            _ => null,
        };
        Player.SetPause(value);
        return new();
    }

    Response GetVol()
    {
        return new("volume"u8, Player.Volume.ToString());
    }

    Response SetVol(int vol)
    {
        Player.Volume = vol;
        return new();
    }

    Response PlaylistInfo()
    {
        Response response = new();

        int i = 0;
        foreach (var song in Player.Queue)
        {
            var itemResponse = song.ToResponse(i);
            response.Extend(itemResponse);
            i++;
        }

        return response;
    }

    Response PlChanges(int version)
    {
        // Naive implementation
        if (version < Player.PlaylistVersion)
        {
            return PlaylistInfo();
        }
        else
        {
            return new();
        }
    }

    static Response TagTypes()
    {
        Response response = new();

        foreach (var tag in Enum.GetValues<Tag>())
        {
            response.Add("tagtype"u8, tag.ToString());
        }

        return response;
    }
}