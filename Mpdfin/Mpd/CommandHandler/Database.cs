using DistIL.Attributes;
using Serilog;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    [Optimize]
    Response List(Tag tag)
    {
        var key = tag.ToU8String();
        var values = Db.GetUniqueTagValues(tag);
        var response = new Response();

        return values.Aggregate(response, (r, v) => r.Append(key, v));
    }

    [Optimize]
    Response Find(List<Filter> filters)
    {
        return Db
            .GetMatchingItems(filters)
            .Select(i => i.GetResponse())
            .Aggregate(new Response(), (r, i) => r.Extend(i));
    }

    [Optimize]
    Response LsInfo(U8String uri)
    {
        var parts = uri.Split((byte)'/', U8SplitOptions.RemoveEmpty);

        var rootNode = Db.FilesystemRoot;
        var pathBuilder = new InlineU8Builder();
        foreach (var part in parts)
        {
            rootNode = rootNode.Navigate(part);
            pathBuilder.AppendFormatted(part);
            if (rootNode is null)
                return new();
        }
        var path = pathBuilder.Written;

        Log.Debug($"Navigated to node with {rootNode.Children.Count} items");

        Response response = new();
        foreach (var node in rootNode.Children)
        {
            if (node.ItemId is Guid idValue)
            {
                response.Extend(Db.Items.First(i => i.Id == idValue).GetResponse());
            }
            else if (node.Name is U8String nameValue)
            {
                if (path.IsEmpty)
                {
                    response.Append("directory"u8, nameValue);
                }
                else
                {
                    response.Append("directory"u8, $"{path}/{nameValue}");
                }
            }
        }

        Log.Debug($"Navigated to {rootNode.Name}");

        pathBuilder.Dispose();
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

        return new Response().Append("updating_db"u8, UpdateJobId);
    }
}
