using System.Collections;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    Response Add(string uri, string? pos)
    {
        if (uri.Length == 0)
        {
            var items = Db.Items.OrderItems().Select(item => (item, Db.GetAudioStreamUri(item.Id))).ToArray();
            Player.AddMany(items);
        }
        else
        {
            AddId(Guid.Parse(uri), pos);
        }

        return new();
    }

    Response AddId(Guid uri, string? pos)
    {
        int? parsedPos = null;
        if (pos is not null)
        {
            parsedPos = pos[0] switch
            {
                '+' => Player.QueuePos + int.Parse(pos[1..]),
                '-' => Player.QueuePos - int.Parse(pos[1..]),
                _ => int.Parse(pos),
            };
        }

        var item = Db.Items.Find(item => item.Id == uri);

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

    Response Shuffle(string? range)
    {
        int start;
        int end;

        if (range is not null)
        {
            var items = range.Split(':', 2);
            try
            {
                start = int.Parse(items[0]);
                end = int.Parse(items[1]);
            }
            catch
            {
                throw new Exception($"Invalid range {range}");
            }
        }
        else
        {
            start = 0;
            end = Player.Queue.Count - 1;
        }

        Player.ShuffleQueue(start, end);

        return new();
    }

    Response Random(int random)
    {
        Player.Random = random == 1;
        return new();
    }
}