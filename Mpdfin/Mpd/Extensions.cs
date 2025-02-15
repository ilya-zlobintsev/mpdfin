using System.Buffers.Text;
using DistIL.Attributes;
using Jellyfin.Sdk.Generated.Models;
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

    [Optimize]
    public static Response GetResponse(this Player.QueueItem item, Database db)
    {
        var song = db.GetItem(item.SongId) ?? throw new Exception("ID not found in database");
        var response = song.GetResponse();

        response.Append("Pos"u8, item.Position);
        response.Append("Id"u8, item.Id);

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
        var response = new Response();
        var tags = U8Enum.GetNames<Tag>().Zip(U8Enum.GetValues<Tag>());

        response.Append("file"u8, item.Id!.Value);

        foreach (var (key, tag) in tags)
        {
            var value = item.GetTagValue(tag);
            response.Append(key, value);
        }

        var duration = item.GetDuration();
        if (duration is not null)
        {
            response.Append("duration"u8, duration.Value);
            response.Append("time"u8, (int)duration.Value);
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
    public static IOrderedEnumerable<U8String> GetUniqueTagValues(this Database db, Tag tag)
    {
        return db.Items.SelectMany(item => item.GetTagValue(tag) ?? []).Distinct().Order();
    }

    [Optimize]
    public static IOrderedEnumerable<BaseItemDto> GetMatchingItems(this Database db, Tag tag, U8String value)
    {
        return db.Items
            .Where(item => item.GetTagValue(tag)?.Contains(value) ?? false)
            .OrderItems();
    }

    [Optimize]
    public static IOrderedEnumerable<BaseItemDto> GetMatchingItems(this Database db, List<Filter> filters)
    {
        return db.Items.Where(item => filters.All(item.MatchesFilter)).OrderItems();
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

    public static Guid ParseGuid(this U8String value)
    {
        return Utf8Parser.TryParse(value, out Guid id, out _)
            ? id : throw new FormatException("Invalid Guid");
    }
}
