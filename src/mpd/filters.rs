use jellyfin_client::model::{Audio, Item, MusicAlbum};

use super::model::{Filter, TagType};

pub fn artist_matches(artist: &Item, filters: &Vec<Filter>, ignore_case: bool) -> bool {
    for filter in filters {
        if !(match filter.tag {
            TagType::Artist | TagType::ArtistSort => {
                if ignore_case {
                    artist
                        .name
                        .to_lowercase()
                        .contains(&filter.expression.to_lowercase())
                } else {
                    artist.name.contains(&filter.expression)
                }
            }
            _ => false,
        }) {
            return false;
        }
    }
    true
}

pub fn album_matches(album: &MusicAlbum, filters: &Vec<Filter>, ignore_case: bool) -> bool {
    for filter in filters {
        if !(match filter.tag {
            TagType::Artist | TagType::ArtistSort => {
                if ignore_case {
                    album
                        .album_artist
                        .iter()
                        .any(|artist| artist.to_lowercase() == filter.expression.to_lowercase())
                } else {
                    album.artists.contains(&filter.expression)
                }
            }
            TagType::AlbumArtist => {
                if ignore_case {
                    album
                        .album_artist
                        .as_ref()
                        .map(|s| s.to_lowercase())
                        .as_ref()
                        == Some(&filter.expression.to_lowercase())
                } else {
                    album.album_artist.as_ref() == Some(&filter.expression)
                }
            }
            TagType::Album => {
                if ignore_case {
                    album
                        .name
                        .to_lowercase()
                        .contains(&filter.expression.to_lowercase())
                } else {
                    album.name.contains(&filter.expression)
                }
            }
            _ => false,
        }) {
            return false;
        }
    }
    true
}

pub fn song_matches(song: &Audio, filters: &Vec<Filter>, ignore_case: bool) -> bool {
    for filter in filters {
        if !(match filter.tag {
            TagType::Artist | TagType::ArtistSort | TagType::Performer => {
                if ignore_case {
                    song.artists
                        .iter()
                        .any(|s| s.to_lowercase() == filter.expression.to_lowercase())
                } else {
                    song.artists.contains(&filter.expression)
                }
            }
            TagType::AlbumArtist => {
                if ignore_case {
                    song.album_artist.as_ref().map_or(false, |album_artist| {
                        album_artist.to_lowercase() == filter.expression.to_lowercase()
                    })
                } else {
                    song.album_artist.as_ref() == Some(&filter.expression)
                }
            }
            TagType::Album => song.album.as_ref().map_or(false, |album| {
                if ignore_case {
                    album.to_lowercase() == filter.expression.to_lowercase()
                } else {
                    album == &filter.expression
                }
            }),
            TagType::Title => {
                if ignore_case {
                    song.name
                        .to_lowercase()
                        .contains(&filter.expression.to_lowercase())
                } else {
                    song.name.contains(&filter.expression)
                }
            }
            _ => false,
        }) {
            return false;
        }
    }
    true
}
