using Jellyfin.Sdk.Generated.Models;

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
    public static U8String[]? GetTagValue(this BaseItemDto item, Tag tag)
    {
        return tag switch
        {
            Tag.Title => item.Name?.ToSingleItemArray(),
            Tag.Album => item.Album?.ToSingleItemArray(),
            Tag.Artist => item.Artists?.Select(U8String.Create).ToArray(),
            Tag.AlbumArtist => item.AlbumArtist?.ToSingleItemArray(),
            Tag.Genre => item.Genres?.Select(U8String.Create).ToArray(),
            Tag.Date => item.GetDate()?.ToSingleItemArray(),
            Tag.Track => item.IndexNumber?.ToU8String().ToSingleItemArray(),
            _ => null,
        };
    }

    static U8String? GetDate(this BaseItemDto item)
    {
        DateTimeOffset?[] possibleDates = [item.PremiereDate, item.EndDate];
        foreach (var possibleDate in possibleDates)
            if (possibleDate is not null && possibleDate.Value.Year != 1)
                return possibleDate.Value.ToU8String("yyyy-MM-dd");

        return item.ProductionYear?.ToU8String();
    }
}

