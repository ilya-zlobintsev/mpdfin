using System.Net;
using Jellyfin.Sdk;
using Serilog;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    Response List(Tag tag)
    {
        var values = Db.GetUniqueTagValues(tag);
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
        Response response = new();

        Db.Items.FindAll(item => filters.All(filter => item.MatchesFilter(filter))).ForEach(item =>
        {
            var itemResponse = item.GetResponse();
            response.Extend(itemResponse);
        });

        return response;
    }

    Response LsInfo(string? uri)
    {
        var parts = uri?.Split("/").Where(item => item.Length > 0).ToArray() ?? Array.Empty<string>();
        Response response = new();

        switch (parts.Length)
        {
            case 0:
                {
                    foreach (var artist in Db.GetUniqueTagValues(Tag.Artist))
                    {
                        response.Add("directory"u8, artist);
                    }
                    break;
                }
            case 1:
                {
                    var artist = parts[0];
                    var albums = Db.GetMatchingItems(Tag.Artist, artist).Select(item => item.Album).Distinct();
                    foreach (var album in albums)
                    {
                        response.Add("directory"u8, $"{artist}/{album}");
                    }
                    break;
                }
            case 2:
                {
                    var filters = new Filter[] { new(Tag.Artist, parts[0]), new(Tag.Album, parts[1]) };
                    foreach (var item in Db.GetMatchingItems(filters))
                    {
                        response.Extend(item.GetResponse());
                    }

                    break;
                }
            default:
                throw new Exception("Path not found");
        }

        return response;
    }
}
