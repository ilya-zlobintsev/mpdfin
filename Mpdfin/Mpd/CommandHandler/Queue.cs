namespace Mpdfin.Mpd;

partial class CommandHandler
{
    Response Add(string uri, string? pos)
    {
        AddId(uri, pos);
        return new();
    }

    Response AddId(string uri, string? pos)
    {
        int? parsedPos = null;
        if (pos is not null)
        {
            parsedPos = pos[0] switch
            {
                '+' => Player.CurrentPos + int.Parse(pos[1..]),
                '-' => Player.CurrentPos - int.Parse(pos[1..]),
                _ => int.Parse(pos),
            };
        }

        var guid = Guid.Parse(uri);
        var item = Db.Items.Find(item => item.Id == guid);

        if (item is not null)
        {
            var url = Db.GetAudioStreamUri(item.Id);
            var queueId = Player.Add(url, item, parsedPos);
            return new("Id"u8, queueId.ToString());
        }
        else
        {
            throw new FileNotFoundException($"Item {uri} not found");
        }
    }

    Response PlaylistInfo()
    {
        Response response = new();

        int i = 0;
        foreach (var song in Player.Queue)
        {
            var itemResponse = song.GetResponse(i);
            response.Extend(itemResponse);
            i++;
        }

        return response;
    }

    Response PlChanges(long version)
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

    Response Clear()
    {
        Player.ClearQueue();
        return new();
    }
}