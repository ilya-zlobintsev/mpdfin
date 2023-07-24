using Jellyfin.Sdk;

namespace Mpdfin.Player;

public readonly struct Song
{
    public readonly int Id;
    public readonly Uri Uri;
    public readonly BaseItemDto Item;

    public Song(Uri uri, BaseItemDto item, int id)
    {
        Uri = uri;
        Item = item;
        Id = id;
    }
}
