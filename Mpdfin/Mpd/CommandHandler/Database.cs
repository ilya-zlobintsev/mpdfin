using FastCache;
using FastCache.Services;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    Response List(Tag tag)
    {
        var values = Db.Items.SelectMany(item => item.GetTagValue(tag)!).Distinct();
        var key = Enum.GetName(tag)!.ToU8String();

        Response response = new();

        foreach (var value in values)
        {
            response.Add(key, value);
        }

        return response;
    }

    Response Find(List<Filter> filters)
    {
        // CacheManager.QueueFullClear<Command, Response>
        // if (Cached<Response>.TryGet(Command.find, filters, out var cached))
        // {
        //     return cached;
        // }

        Response response = new();

        Db.Items.FindAll(item => filters.All(filter => item.MatchesFilter(filter))).ForEach(item =>
        {
            var itemResponse = item.GetResponse();
            response.Extend(itemResponse);
        });

        // cached.Save(response, TimeSpan.FromMinutes(5));
        return response;
    }
}
