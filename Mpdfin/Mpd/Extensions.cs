using Jellyfin.Sdk;
using Mpdfin.Player;
using Mpdfin.DB;

namespace Mpdfin.Mpd;

static class Extensions
{
    public readonly struct AsyncLock : IDisposable
    {
        public required SemaphoreSlim Lock { get; init; }

        public void Dispose() => Lock.Release();
    }

    public static async ValueTask<AsyncLock> LockAsync(this SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        return new AsyncLock { Lock = semaphore };
    }

    public static Response GetResponse(this Song song, int? pos)
    {
        var response = song.Item.GetResponse();

        if (pos is not null)
            response.Add("Pos"u8, pos.Value.ToU8String());

        response.Add("Id"u8, song.Id.ToU8String());

        return response;
    }

    /// <summary>
    /// Gets the item duration in seconds.
    /// </summary>
    public static double? GetDuration(this BaseItemDto item)
    {
        if (item.RunTimeTicks is not null)
            return (double)item.RunTimeTicks / 10000000;
        else
            return null;
    }

    public static Response GetResponse(this BaseItemDto item)
    {
        Response response = new();

        response.Add("file"u8, item.Id.ToU8String());

        foreach (var tag in Enum.GetValues<Tag>())
        {
            var key = Enum.GetName(tag)!.ToU8String();
            var value = item.GetTagValue(tag);
            response.Add(key, value);
        }

        var duration = item.GetDuration();
        if (duration is not null)
        {
            response.Add("duration"u8, duration.Value.ToU8String());
            response.Add("time"u8, ((int)duration.Value).ToU8String());
        }

        return response;
    }

    public static string[] ToSingleItemArray(this string value)
    {
        return new string[1] { value };
    }

    public static IEnumerable<string> GetUniqueTagValues(this Database db, Tag tag)
    {
        return db.Items.SelectMany(item => item.GetTagValue(tag) ?? Array.Empty<string>()).Distinct().Order();
    }

    public static IEnumerable<BaseItemDto> GetMatchingItems(this Database db, Tag tag, string? value)
    {
        return db.Items.FindAll(item => (item.GetTagValue(tag) ?? Array.Empty<string>()).Any(itemValue => itemValue == value)).OrderItems();
    }

    public static IEnumerable<BaseItemDto> GetMatchingItems(this Database db, Filter[] filters)
    {
        return db.Items.FindAll(item => filters.All(filter => item.MatchesFilter(filter))).OrderItems();
    }

    public static IOrderedEnumerable<BaseItemDto> OrderItems(this IEnumerable<BaseItemDto> items)
    {
        return items.OrderBy(item => (item.AlbumArtist, item.Artists.ElementAtOrDefault(0), item.Album, item.IndexNumber, item.PremiereDate, item.Name));
    }
}
