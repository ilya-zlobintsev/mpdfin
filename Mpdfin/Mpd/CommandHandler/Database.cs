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

        foreach (var item in Db.GetMatchingItems(filters.ToArray()))
        {
            response.Extend(item.GetResponse());
        }

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
                        if (artist is not null)
                            response.Add("directory"u8, artist);
                    }

                    foreach (var item in Db.GetMatchingItems(Tag.Artist, null))
                    {
                        response.Extend(item.GetResponse());
                    }

                    break;
                }
            case 1:
                {
                    var artist = parts[0];
                    var albums = Db.GetMatchingItems(Tag.Artist, artist).Select(item => item.Album).Distinct();
                    foreach (var album in albums)
                    {
                        if (artist is not null && album is not null)
                            response.Add("directory"u8, $"{artist}/{album}");
                    }

                    var filters = new Filter[] { new(Tag.Artist, artist), new(Tag.Album, null) };
                    var itemsWithoutAlbum = Db.GetMatchingItems(filters);
                    foreach (var item in itemsWithoutAlbum)
                    {
                        response.Extend(item.GetResponse());
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
