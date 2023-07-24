using Jellyfin.Sdk;
using LibVLCSharp.Shared;
using Mpdfin.Player;

namespace Mpdfin.Mpd;

static class Extensions
{
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
