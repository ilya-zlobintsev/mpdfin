using Jellyfin.Sdk;
using Mpdfin.Player;
using Serilog;

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

        return response;
    }

    public static string[] ToSingleItemArray(this string value)
    {
        return new string[1] { value };
    }

    public static IEnumerable<string> GetUniqueValues(this Database db, Tag tag)
    {
        return db.Items.SelectMany(item => item.GetTagValue(tag) ?? Array.Empty<string>()).Distinct();
    }

    // public static bool ParseEnum<TEnum>(string value, out TEnum result) where TEnum : struct
    // {
    //     if (!int.TryParse(value, out int _) && Enum.TryParse(value, false, out TEnum parsed))
    //     {
    //         result = parsed;
    //         return true;
    //     }
    //     else
    //     {
    //         return false;
    //     }
    // }
}
