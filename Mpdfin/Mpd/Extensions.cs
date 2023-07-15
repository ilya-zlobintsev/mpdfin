using Mpdfin.Player;

namespace Mpdfin.Mpd;

static class Extensions
{
    public static Response ToResponse(this Song song, int pos)
    {
        Response itemResponse = new();

        var item = song.Item;

        itemResponse.Add("file"u8, item.Id.ToU8String());
        itemResponse.Add("Title"u8, item.Name);

        foreach (var artist in item.Artists)
        {
            itemResponse.Add("Artist"u8, artist);
        }

        foreach (var albumArtist in item.AlbumArtists)
        {
            itemResponse.Add("AlbumArtist"u8, albumArtist.Name);
        }

        itemResponse.Add("Pos"u8, pos.ToU8String());
        itemResponse.Add("Id"u8, song.Id.ToU8String());

        return itemResponse;
    }
}