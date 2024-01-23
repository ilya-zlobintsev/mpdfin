using System.Text;
using DistIL.Attributes;
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
            response.Append(keyBytes, value);
        }

        return response;
    }

    [Optimize]
    Response Find(List<Filter> filters)
    {
        return Db
            .GetMatchingItems(filters)
            .Aggregate(new Response(), (response, item) => response.Extend(item.GetResponse()));
    }

    [Optimize]
    Response LsInfo(U8String uri)
    {
        var parts = uri.Split('/', U8SplitOptions.RemoveEmpty);

        var rootNode = Db.FilesystemRoot;
        var pathBuilder = new InterpolatedU8StringHandler();
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
            if (node.ItemId is not null)
            {
                var item = Db.Items.Find(item => item.Id == node.ItemId)!;
                response.Extend(item.GetResponse());
            }
            else if (node.Name is U8String nameValue)
            {
                var value = !path.IsEmpty ? u8($"{path}/{nameValue}") : nameValue;
                response.Append("directory"u8, value);
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
