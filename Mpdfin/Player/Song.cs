using Jellyfin.Sdk;

namespace Mpdfin;

readonly struct Song
{
    public readonly Guid Id;
    public readonly Uri Uri;
    public readonly BaseItemDto Item;

    public Song(Uri uri, BaseItemDto item)
    {
        Uri = uri;
        Item = item;
        Id = Guid.NewGuid();
    }

    public Response GetResponse()
    {
        Response response = new();

        response.Add("file"u8, Item.Id.ToString());
        response.Add("Title"u8, Item.Name);

        foreach (var artist in Item.Artists)
        {
            response.Add("Artist"u8, artist);
        }

        foreach (var albumArtist in Item.AlbumArtists)
        {
            response.Add("AlbumArtist"u8, albumArtist.Name);
        }

        return response;
    }
}
