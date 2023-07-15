using Jellyfin.Sdk;

namespace Mpdfin.Player;

public readonly struct Song
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
}
