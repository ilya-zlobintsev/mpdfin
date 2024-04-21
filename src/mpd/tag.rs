use crate::mpd::Result;
use crate::{jellyfin::base::BaseItemDto, mpd::Error};
use std::borrow::Cow;
use std::str::FromStr;
use strum::{Display, EnumIter, EnumString};

#[derive(EnumString, Display, Debug, PartialEq, Eq, EnumIter, Clone, Copy)]
#[strum(ascii_case_insensitive)]
pub enum Tag {
    Artist,
    Album,
    AlbumArtist,
    Title,
    Track,
    Name,
    Genre,
    Mood,
    Date,
    OriginalDate,
    Composer,
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
    // MusicbrainzArtistId,
    // MusicbrainzAlbumid,
    // MusicbrainzAlbumArtistId,
    // MusicbrainzTrackId,
    // MusicbrainzReleaseGroupId,
    // MusicbrainzReleaseTrackId,
    // MusicbrainzWorkId,
}

impl Tag {
    /// [`from_str`] with the mpd error
    pub fn try_from_str(value: &str) -> Result<Self> {
        Self::from_str(value).map_err(|_| Error::InvalidArg(format!("Unknown tag type '{value}'")))
    }
}

impl BaseItemDto {
    pub fn get_tag_values(&self, tag: Tag) -> Option<Vec<Cow<'_, str>>> {
        match tag {
            Tag::Artist => Some(
                self.artists
                    .iter()
                    .map(|item| item.as_ref().into())
                    .collect(),
            ),
            Tag::Album => self.album.as_ref().map(|album| vec![album.as_ref().into()]),
            Tag::AlbumArtist => self.album_artist.as_ref().map(|album| vec![album.into()]),
            Tag::Title | Tag::Name => self.name.as_ref().map(|name| vec![name.as_ref().into()]),
            Tag::Date => self
                .premiere_date
                .map(|date| vec![date.format("%Y-%m-%d").to_string().into()]),
            Tag::Genre => Some(
                self.genres
                    .iter()
                    .map(|genre| genre.as_str().into())
                    .collect(),
            ),
            _ => None,
        }
    }
}
