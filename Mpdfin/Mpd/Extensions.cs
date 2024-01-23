using DistIL.Attributes;
using Jellyfin.Sdk;
using Mpdfin.DB;
using System.Text;

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

    [Optimize]
    public static Response GetResponse(this Player.QueueItem item, Database db)
    {
        var song = db.GetItem(item.SongId) ?? throw new Exception("ID not found in database");
        var response = song.GetResponse();

        response.Add("Pos"u8, item.Position.ToString());
        response.Add("Id"u8, item.Id.ToString());

        return response;
    }

    /// <summary>
    /// Gets the item duration in seconds.
    /// </summary>
    public static double? GetDuration(this BaseItemDto item)
    {
        return item.RunTimeTicks is not null
            ? (double)item.RunTimeTicks / 10000000
            : null;
    }

    public static Response GetResponse(this BaseItemDto item)
    {
        Response response = new();

        response.Add("file"u8, item.Id.ToString());

        foreach (var (key, tag) in Constants.TagNames.Zip(Constants.TagValues))
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var value = item.GetTagValue(tag);
            response.Add(keyBytes, value);
        }

        var duration = item.GetDuration();
        if (duration is not null)
        {
            response.Add("duration"u8, duration.Value.ToString());
            response.Add("time"u8, ((int)duration.Value).ToString());
        }

        return response;
    }

    public static U8String[] ToSingleItemArray(this string value)
    {
        return [u8(value)];
    }

    public static U8String[] ToSingleItemArray(this U8String value)
    {
        return [u8(value)];
    }

    [Optimize]
    public static IOrderedEnumerable<string> GetUniqueTagValues(this Database db, Tag tag)
    {
        return db.Items.SelectMany(item => item.GetTagValue(tag) ?? []).Distinct().Order();
    }

    [Optimize]
    public static IOrderedEnumerable<BaseItemDto> GetMatchingItems(this Database db, Tag tag, string? value)
    {
        return db.Items.FindAll(item => (item.GetTagValue(tag) ?? []).Any(itemValue => itemValue == value)).OrderItems();
    }

    [Optimize]
    public static IOrderedEnumerable<BaseItemDto> GetMatchingItems(this Database db, Filter[] filters)
    {
        return db.Items.FindAll(item => filters.All(filter => item.MatchesFilter(filter))).OrderItems();
    }

    [Optimize]
    public static IOrderedEnumerable<BaseItemDto> OrderItems(this IEnumerable<BaseItemDto> items)
    {
        return items.OrderBy(item => (
            item.AlbumArtist,
            item.Artists is [var first, ..] ? first : null,
            item.Album,
            item.IndexNumber,
            item.PremiereDate,
            item.Name));
    }
}
