using Jellyfin.Sdk;

namespace Mpdfin.Mpd;

enum Tag
{
    Artist,
    ArtistSort,
    Album,
    AlbumSort,
    AlbumArtist,
    AlbumArtistSort,
    Title,
    TitleSort,
    Track,
    Name,
    Genre,
    Mood,
    Date,
    OriginalDate,
    Composer,
    ComposerSort,
    Performer,
    Conductor,
    Work,
    Ensemble,
    Movement,
    MovementNumber,
    Location,
    Grouping,
    Comment,
    Disc,
    Label,

    MUSICBRAINZ_ARTISTID,
    MUSICBRAINZ_ALBUMARTISTID,
    MUSICBRAINZ_RELEASETRACKID,
    MUSICBRAINZ_WORKID,
}

static class TagExtractor
{
    public static string[]? GetTagValue(this BaseItemDto item, Tag tag)
    {
        static string? Sanitize(string? value)
        {
            return value?.Replace('/', '+');
        }

        return tag switch
        {
            Tag.Title => item.Name?.ToSingleItemArray(),
            Tag.Album => Sanitize(item.Album)?.ToSingleItemArray(),
            Tag.Artist => item.Artists?.Select(artist => Sanitize(artist)!)?.ToArray(),
            Tag.AlbumArtist => Sanitize(item.AlbumArtist)?.ToSingleItemArray(),
            Tag.Genre => item.Genres?.ToArray(),
            Tag.Date => item.PremiereDate?.ToString("yyyy-MM-dd").ToSingleItemArray(),
            Tag.Track => item.IndexNumber?.ToString().ToSingleItemArray(),
            _ => null,
        };
    }
}
