using Jellyfin.Sdk;
using Mpdfin.Mpd;
using Serilog;

namespace Mpdfin.DB;

public record class Node
{
    public string? Name { get; }
    public Guid? ItemId { get; }
    public List<Node> Children { get; private set; }

    public Node(string name)
    {
        Name = Sanitize(name);
        Children = new();
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
        foreach (var child in Children)
        {
            if (child.Name == name)
            {
                return child;
            }
        }
        return null;
    }

    public static Node BuildTree(Database db)
    {
        List<Node> rootNodes = new();

        foreach (var artist in db.GetUniqueTagValues(Tag.Artist))
        {
            var albums = db.GetMatchingItems(Tag.Artist, artist)
                .SelectMany(item => item.GetTagValue(Tag.Album) ?? [])
                .Distinct();

            List<Node> albumNodes = new();
            foreach (var album in albums)
            {
                Filter[] albumFilters = [new Filter(Tag.Artist, artist), new Filter(Tag.Album, album)];
                var songs = db.GetMatchingItems(albumFilters);

                List<Node> songNodes = new();
                foreach (var song in songs)
                {
                    songNodes.Add(new(song));
                }

                albumNodes.Add(new(album, songNodes));
            }

            rootNodes.Add(new(artist, albumNodes));
        }

        return new(rootNodes);
    }

    static string Sanitize(string value)
    {
        return value.Replace('/', '+');
    }
}
