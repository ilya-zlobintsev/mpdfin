using DistIL.Attributes;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    [Optimize]
    Response Add(U8String uri, U8String pos)
    {
        if (uri.Length == 0)
        {
            var items = Db.Items.OrderItems().Select(song => song.Id!.Value).ToArray();
            Player.AddMany(items);
        }
        else
        {
            AddId(uri.ParseGuid(), pos);
        }

        return new();
    }

    [Optimize]
    Response AddId(Guid uri, U8String pos)
    {
        int? parsedPos = null;
        if (pos.Length > 0)
        {
            parsedPos = pos[0] switch
            {
                (byte)'+' => Player.CurrentPos + int.Parse(pos[1..]),
                (byte)'-' => Player.CurrentPos - int.Parse(pos[1..]),
                _ => int.Parse(pos),
            };
        }

        var song = Db.Items.FirstOrDefault(item => item.Id == uri);
        if (song is not null)
        {
            var queueId = Player.Add(song.Id!.Value, parsedPos);
            return new Response().Append("Id"u8, queueId);
        }

        throw new FileNotFoundException($"Item {uri} not found");
    }

    Response Delete(U8String input)
    {
        if (int.TryParse(input, out var pos))
        {
            Player.DeletePos(pos);
        }
        else
        {
            var queueSlice = Request.ParseRange(input);
            Player.DeleteRange(queueSlice);
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
        return version < Player.PlaylistVersion ? PlaylistInfo() : new();
    }

    Response Clear()
    {
        Player.ClearQueue();
        return new();
    }

    Response Shuffle(U8String? range)
    {
        var queueSlice = range.HasValue
            ? Request.ParseRange(range.Value)
            : new(0, Player.Queue.Count - 1);

        Player.ShuffleQueue(queueSlice);

        return new();
    }

    Response Random(int random)
    {
        Player.SetRandom(random == 1);
        return new();
    }
}
