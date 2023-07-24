using FastCache;

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

    Response Find(Tag tag, string value)
    {
        if (Cached<Response>.TryGet(Command.find, tag, value, out var cached))
        {
            return cached;
        }

        Response response = new();

        Db.Items.FindAll(item => item.GetTagValue(tag)!.Any(tagValue => tagValue == value)).ForEach(item =>
        {
            var itemResponse = item.GetResponse();
            response.Extend(itemResponse);
        });

        cached.Save(response, TimeSpan.FromMinutes(5));
        return response;
    }
}