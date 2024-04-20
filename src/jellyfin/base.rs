use chrono::Utc;
use serde::{Deserialize, Serialize};
use std::rc::Rc;

#[derive(Deserialize)]
#[serde(rename_all = "PascalCase")]
pub struct ItemsResponse {
    pub items: Vec<BaseItemDto>,
    pub total_record_count: i32,
    pub start_index: i32,
}

#[derive(Serialize, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub struct BaseItemDto {
    pub id: Rc<str>,
    pub name: Option<Rc<str>>,
    pub r#type: BaseItemKind,
    pub collection_type: Option<String>,
    pub album: Option<Rc<str>>,
    #[serde(default)]
    pub artists: Vec<Rc<str>>,
    pub album_artist: Option<String>,
    #[serde(default)]
    pub genres: Vec<String>,
    pub index_number: Option<u32>,
    pub premiere_date: Option<chrono::DateTime<Utc>>,
}

#[derive(Serialize, Deserialize, Debug)]
#[serde(rename_all = "PascalCase")]
pub enum BaseItemKind {
    /// Item is aggregate folder.
    AggregateFolder,

    /// Item is audio.
    Audio,

    /// Item is audio book.
    AudioBook,

    /// Item is base plugin folder.
    BasePluginFolder,

    /// Item is book.
    Book,

    /// Item is box set.
    BoxSet,

    /// Item is channel.
    Channel,

    /// Item is channel folder item.
    ChannelFolderItem,

    /// Item is collection folder.
    CollectionFolder,

    /// Item is episode.
    Episode,

    /// Item is folder.
    Folder,

    /// Item is genre.
    Genre,

    /// Item is manual playlists folder.
    ManualPlaylistsFolder,

    /// Item is movie.
    Movie,

    /// Item is a live tv channel.
    LiveTvChannel,

    /// Item is a live tv program.
    LiveTvProgram,

    /// Item is music album.
    MusicAlbum,

    /// Item is music artist.
    MusicArtist,

    /// Item is music genre.
    MusicGenre,

    /// Item is music video.
    MusicVideo,

    /// Item is person.
    Person,

    /// Item is photo.
    Photo,

    /// Item is photo album.
    PhotoAlbum,

    /// Item is playlist.
    Playlist,

    /// Item is playlist folder.
    PlaylistsFolder,

    /// Item is program.
    Program,

    /// Item is recording.
    /// <remarks>
    /// Manually added.
    /// </remarks>
    Recording,

    /// Item is season.
    Season,

    /// Item is series.
    Series,

    /// Item is studio.
    Studio,

    /// Item is trailer.
    Trailer,

    /// Item is live tv channel.
    /// <remarks>
    /// Type is overridden.
    /// </remarks>
    TvChannel,

    /// Item is live tv program.
    /// <remarks>
    /// Type is overridden.
    /// </remarks>
    TvProgram,

    /// Item is user root folder.
    UserRootFolder,

    /// Item is user view.
    UserView,

    /// Item is video.
    Video,

    /// Item is year.
    Year,
}
