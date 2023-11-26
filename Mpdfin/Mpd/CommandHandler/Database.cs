using System.Diagnostics.CodeAnalysis;
using System.Text;
using DistIL.Attributes;
using Mpdfin.DB;
using Serilog;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    [Optimize]
    Response List(Tag tag)
    {
        var values = Db.GetUniqueTagValues(tag);
        var key = Enum.GetName(tag)!;
        var keyBytes = Encoding.UTF8.GetBytes(key);

        Response response = new();

        foreach (var value in values)
        {
            response.Add(keyBytes, value);
        }

        return response;
    }

    [Optimize]
    Response Find(List<Filter> filters)
    {
        return Db
            .GetMatchingItems([..filters])
            .Aggregate(new Response(), (response, item) => response.Extend(item.GetResponse()));
    }

    [Optimize]
    Response LsInfo(string? uri)
    {
        var parts = uri?.Split("/", StringSplitOptions.RemoveEmptyEntries) ?? [];

        var rootNode = Db.FilesystemRoot;
        StringBuilder pathBuilder = new();
        foreach (var part in parts)
        {
            rootNode = rootNode.Navigate(part);
            pathBuilder.Append(part);
            if (rootNode is null)
                return new();
        }
        var path = pathBuilder.ToString();

        Log.Debug($"Navigated to node with {rootNode.Children.Count} items");

        Response response = new();
        foreach (var node in rootNode.Children)
        {
            if (node.ItemId is not null)
            {
                var item = Db.Items.Find(item => item.Id == node.ItemId)!;
                response.Extend(item.GetResponse());
            }
            else if (node.Name is not null)
            {
                var value = !string.IsNullOrEmpty(path)
                    ? $"{path}/{node.Name}"
                    : node.Name;
                response.Add("directory"u8, value);
            }
        }

        Log.Debug($"Navigated to {rootNode.Name}");

        /*switch (parts.Length)
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
                    var albums = Db.GetMatchingItems(Tag.Artist, artist).SelectMany(item => item.GetTagValue(Tag.Album) ?? Array.Empty<string>()).Distinct();
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
        }*/

        return response;
    }

    Response Update()
    {
        UpdateJobId++;
        Updating = true;

        _ = Task.Run(async () =>
        {
            try
            {
                await Db.Update();
            }
            catch (Exception ex)
            {
                Log.Warning($"Could not update db: {ex}");
            }
            finally
            {
                Updating = false;
            }
        });

        return new("updating_db"u8, UpdateJobId.ToString());
    }
}
