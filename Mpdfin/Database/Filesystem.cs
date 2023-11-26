using DistIL.Attributes;
using Jellyfin.Sdk;
using Mpdfin.Mpd;

namespace Mpdfin.DB;

public record Node
{
    public string? Name { get; }
    public Guid? ItemId { get; }
    public List<Node> Children { get; private set; }

    public Node(string name)
    {
        Name = Sanitize(name);
        Children = [];
    }

    public Node(BaseItemDto item) : this(item.Name)
    {
        ItemId = item.Id;
    }

    public Node(string name, List<Node> children) : this(children)
    {
        Name = Sanitize(name);
    }

    public Node(List<Node> children)
    {
        Children = children;
    }

    public Node? Navigate(string name)
    {
        return Children.Find(child => child.Name == name);
    }

    [Optimize]
    public static Node BuildTree(Database db)
    {
        List<Node> rootNodes = [];

        foreach (var artist in db.GetUniqueTagValues(Tag.Artist))
        {
            var albums = db.GetMatchingItems(Tag.Artist, artist)
                .SelectMany(item => item.GetTagValue(Tag.Album) ?? [])
                .Distinct();

            var albumNodes = albums
                .Select(album =>
                {
                    var songs = db.GetMatchingItems(
                    [
                        new(Tag.Artist, artist),
                        new(Tag.Album, album)
                    ]);

                    return new Node(album, songs.Select(song => new Node(song)).ToList());
                })
                .ToList();

            rootNodes.Add(new(artist, albumNodes));
        }

        return new(rootNodes);
    }

    static string Sanitize(string value)
    {
        return value.Replace('/', '+');
    }
}
