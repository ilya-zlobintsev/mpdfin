using System.Collections;
using Microsoft.Win32;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    Response Add(string uri, string? pos)
    {
        if (uri.Length == 0)
        {
            var items = Db.Items.OrderItems().Select(song => song.Id).ToArray();
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
                '+' => Player.CurrentPos + int.Parse(pos[1..]),
                '-' => Player.CurrentPos - int.Parse(pos[1..]),
                _ => int.Parse(pos),
            };
        }

        var song = Db.Items.Find(item => item.Id == uri);

        if (song is not null)
        {
            var url = Db.Client.GetAudioStreamUri(song.Id);
            var queueId = Player.Add(song.Id, parsedPos);
            return new("Id"u8, queueId.ToString());
        }
        else
        {
            throw new FileNotFoundException($"Item {uri} not found");
        }
    }

    Response Delete(string input)
    {
        if (int.TryParse(input, out int pos))
        {
            Player.DeletePos(pos);
        }
        else
        {
            var (start, end) = Request.ParseRange(input);
            Player.DeleteRange(start, end);
        }
        return new();
    }

    Response DeleteId(int id)
    {
        Player.DeleteId(id);
        return new();
    }

    Response PlaylistInfo()
    {
        Response response = new();

        foreach (var item in Player.Queue.AsEnumerable())
        {
            var itemResponse = item.GetResponse(Db);
            response.Extend(itemResponse);
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
            (start, end) = Request.ParseRange(range);
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
        Player.SetRandom(random == 1);
        return new();
    }
}