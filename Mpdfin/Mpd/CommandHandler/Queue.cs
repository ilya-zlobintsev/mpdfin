namespace Mpdfin.Mpd;

partial class CommandHandler
{
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
}