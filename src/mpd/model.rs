use serde::{Deserialize, Serialize};
use std::{fmt::Display, str::FromStr};
use strum::{EnumIter, EnumString, EnumVariantNames};

use super::error::MpdError;
use crate::{
    mpd::{error::Ack, serializer},
    playback_server::queue::Position,
};

#[derive(PartialEq, Eq, Debug, EnumVariantNames)]
#[strum(serialize_all = "lowercase")]
pub enum Command {
    Status,
    CurrentSong,
    PlChanges,
    TagTypes(TagTypeCommand),
    LsInfo(Option<String>),
    Repeat(bool),
    Random(bool),
    Single(bool),
    Consume(bool),
    Playlist,
    PlaylistInfo(Option<PlaylistInfo>),
    Ping,
    Idle(Vec<Subsystem>),
    NoIdle,
    Outputs,
    Decoders,
    Update,
    Add(String, Option<Position>),
    AddId(String, Option<Position>),
    List(TagType, Vec<Filter>),
    ListPlaylists,
    ListPlaylistInfo(String),
    Commands,
    Play,
    PlayId(Option<usize>),
    Pause(Option<bool>),
    Stats,
    UrlHandlers,
    Find(Vec<Filter>),
    Search(Vec<Filter>),
    Volume(i64),
    SetVol(i64),
}

impl FromStr for Command {
    type Err = MpdError;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        let (cmd, args) = extract_cmd(s)?;
        let mut args = args.into_iter();

        Ok(match cmd.as_str() {
            "status" => Command::Status,
            "tagtypes" => {
                let subcmd = args.next();
                let tag_cmd = match subcmd.as_deref() {
                    Some("all") => TagTypeCommand::All,
                    Some("clear") => TagTypeCommand::Clear,
                    Some("enable") => TagTypeCommand::Enable(tag_from_args(&mut args, &cmd)?),
                    Some("disable") => TagTypeCommand::Disable(tag_from_args(&mut args, &cmd)?),
                    None => TagTypeCommand::List,
                    _ => {
                        return Err(MpdError::new(
                            Some(cmd),
                            format!("invalid subcommand: {}", subcmd.unwrap()),
                            Ack::Arg,
                        ))
                    }
                };
                Command::TagTypes(tag_cmd)
            }
            "lsinfo" => Command::LsInfo(args.next()),
            "idle" => {
                let filters = args
                    .map(|s| Subsystem::from_str(&s))
                    .collect::<Result<Vec<Subsystem>, strum::ParseError>>()
                    .map_err(|_| {
                        MpdError::new(Some(cmd), "unknown subsystem".to_owned(), Ack::Arg)
                    })?;
                Command::Idle(filters)
            }
            "noidle" => Command::NoIdle,
            "repeat" | "random" | "single" | "consume" => {
                let arg = args
                    .next()
                    .ok_or_else(|| MpdError::wrong_argument_count(&cmd))?;
                let value = parse_bool(&arg, &cmd)?;
                match cmd.as_str() {
                    "repeat" => Command::Repeat(value),
                    "random" => Command::Random(value),
                    "single" => Command::Single(value),
                    "consume" => Command::Consume(value),
                    _ => unreachable!(),
                }
            }
            "currentsong" => Command::CurrentSong,
            "plchanges" => Command::PlChanges,
            "playlist" => Command::Playlist,
            "playlistinfo" => {
                let maybe_info = match args.next() {
                    Some(arg) => {
                        let split: Vec<&str> = arg.split(":").collect();
                        match split.len() {
                            1 => {
                                let pos = split.first().unwrap().parse()?;
                                Some(PlaylistInfo::SongPos(pos))
                            }
                            2 => {
                                let start = split.first().unwrap().parse()?;
                                let end = split.last().unwrap().parse()?;
                                Some(PlaylistInfo::StartEnd(start, end))
                            }
                            _ => {
                                return Err(MpdError::new(
                                    Some(cmd),
                                    "invalid arguments".to_string(),
                                    Ack::Arg,
                                ))
                            }
                        }
                    }
                    None => None,
                };
                Command::PlaylistInfo(maybe_info)
            }
            "ping" => Command::Ping,
            "outputs" => Command::Outputs,
            "decoders" => Command::Decoders,
            "update" => Command::Update,
            "add" => {
                let item_id = args
                    .next()
                    .ok_or_else(|| MpdError::wrong_argument_count(&cmd))?;
                let pos = match args.next() {
                    Some(s) => Some(parse_position(&s, &cmd)?),
                    None => None,
                };
                Command::Add(item_id, pos)
            }
            "addid" => {
                let item_id = args
                    .next()
                    .ok_or_else(|| MpdError::wrong_argument_count(&cmd))?;
                let pos = match args.next() {
                    Some(s) => Some(parse_position(&s, &cmd)?),
                    None => None,
                };
                Command::AddId(item_id, pos)
            }
            "list" => Command::List(
                tag_from_args(&mut args, &cmd)?,
                parse_filters(&mut args, &cmd)?,
            ),
            "listplaylists" => Command::ListPlaylists,
            "listplaylistinfo" => Command::ListPlaylistInfo(
                args.next()
                    .ok_or_else(|| MpdError::wrong_argument_count(&cmd))?,
            ),
            "commands" => Command::Commands,
            "play" => Command::Play,
            "playid" => Command::PlayId({
                if let Some(id) = args.next() {
                    Some(id.parse().map_err(|_| {
                        MpdError::new(
                            Some(cmd),
                            format!("unsigned integer expected, {} found", id),
                            Ack::Arg,
                        )
                    })?)
                } else {
                    None
                }
            }),
            "pause" => {
                let v = match args.next() {
                    Some(arg) => Some(parse_bool(&arg, &cmd)?),
                    None => None,
                };
                Command::Pause(v)
            }
            "stats" => Command::Stats,
            "urlhandlers" => Command::UrlHandlers,
            "find" => Command::Find(parse_filters(&mut args, &cmd)?),
            "search" => Command::Search(parse_filters(&mut args, &cmd)?),
            "volume" => Command::Volume(
                args.next()
                    .ok_or_else(|| MpdError::wrong_argument_count(&cmd))?
                    .parse()?,
            ),
            "setvol" => Command::SetVol(
                args.next()
                    .ok_or_else(|| MpdError::wrong_argument_count(&cmd))?
                    .parse()?,
            ),
            _ => return Err(MpdError::unknown_command(&cmd)),
        })
    }
}

#[derive(PartialEq, Eq, Debug, EnumVariantNames)]
#[strum(serialize_all = "lowercase")]
pub enum PlaylistInfo {
    SongPos(usize),
    StartEnd(usize, usize),
}

#[derive(PartialEq, Eq, Debug)]
pub enum TagTypeCommand {
    List,
    Enable(TagType),
    Disable(TagType),
    Clear,
    All,
}

#[derive(
    Serialize,
    Deserialize,
    EnumVariantNames,
    EnumString,
    EnumIter,
    PartialEq,
    Eq,
    PartialOrd,
    Ord,
    strum::Display,
    Debug,
)]

pub enum TagType {
    Artist,
    ArtistSort,
    Album,
    AlbumArtist,
    AlbumArtistSort,
    Title,
    Track,
    Name,
    Genre,
    Date,
    Composer,
    Performer,
}

#[derive(Serialize, Deserialize, Debug, Default)]
pub struct Status {
    pub repeat: bool,
    pub random: bool,
    pub single: bool,
    pub consume: bool,
    pub volume: u8,
    pub state: State,
    pub playlist: u32,
    #[serde(rename = "playlistlength")]
    pub playlist_length: i32,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub song: Option<usize>,
    #[serde(rename = "songid", skip_serializing_if = "Option::is_none")]
    pub song_id: Option<usize>,
    #[serde(rename = "nextsong", skip_serializing_if = "Option::is_none")]
    pub next_song: Option<usize>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub elapsed: Option<i32>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub duration: Option<f64>,
}

#[derive(Serialize, Deserialize, Debug, Clone, Copy, PartialEq, Eq)]
#[serde(rename_all = "lowercase")]
pub enum State {
    Play,
    Stop,
    Pause,
}

impl Default for State {
    fn default() -> Self {
        Self::Stop
    }
}

#[derive(Serialize, Clone, Debug)]
#[serde(rename_all = "PascalCase")]
pub struct Song {
    #[serde(flatten)]
    pub entry: SongEntry,
    pub pos: i32,
    pub id: i32,
}

#[derive(Serialize)]
#[serde(rename_all = "lowercase", untagged)]
pub enum ListEntry {
    File(SongEntry),
    Directory(DirectoryEntry),
}

#[derive(Serialize)]
#[serde(rename_all = "lowercase")]
pub struct DirectoryEntry {
    pub directory: String,
}

#[derive(Serialize, Default, Clone, Debug)]
#[serde(rename_all = "PascalCase")]
pub struct SongEntry {
    #[serde(rename = "file")]
    pub filename: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub artist: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub album: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub title: Option<String>,
    #[serde(skip_serializing_if = "Vec::is_empty")]
    pub composer: Vec<String>,
    #[serde(skip_serializing_if = "Vec::is_empty")]
    pub genre: Vec<String>,
    #[serde(rename = "duration")]
    pub duration: f64,
}

impl From<jellyfin_client::model::Audio> for SongEntry {
    fn from(audio: jellyfin_client::model::Audio) -> Self {
        Self {
            filename: audio.id,
            artist: audio.album_artist,
            album: audio.album,
            title: Some(audio.name),
            composer: audio.artists,
            genre: vec![], // TODO
            duration: (audio.runtime_ticks.unwrap_or_default() as f64 / 10000.0).round() / 1000.0,
        }
    }
}

#[derive(Serialize, Clone, Debug)]
#[serde(rename_all = "lowercase")]
pub struct ChangedEvent {
    pub changed: Subsystem,
}

#[derive(Serialize, Clone, Debug, PartialEq, Eq, EnumString)]
#[serde(rename_all = "lowercase")]
#[strum(serialize_all = "snake_case")]
pub enum Subsystem {
    Database,
    Update,
    StoredPlaylist,
    Playlist,
    Player,
    Mixer,
    Output,
    Options,
    Partition,
    Sticker,
    Subscription,
    Message,
    Neighbor,
    Mount,
}

impl Display for Subsystem {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", serializer::to_string(self).unwrap())
    }
}

#[derive(Serialize, Clone, Debug, PartialEq, Eq)]
pub struct Output<'a> {
    #[serde(rename = "outputid")]
    pub output_id: u32,
    #[serde(rename = "outputname")]
    pub output_name: &'a str,
    #[serde(rename = "outputenabled")]
    pub output_enabled: bool,
}

#[derive(Serialize, Clone, Debug, PartialEq, Eq)]
pub struct Decoder<'a> {
    pub plugin: &'a str,
    pub suffix: &'a str,
    #[serde(skip_serializing_if = "Vec::is_empty")]
    pub mime_type: Vec<&'a str>,
}

#[derive(Serialize, Clone, Debug)]
pub struct Update {
    pub updating_db: usize,
}

#[derive(Serialize, Clone, Debug)]
#[serde(rename_all = "PascalCase")]
pub struct AddId {
    pub id: usize,
}

#[derive(Serialize, Clone, Debug)]
pub struct Stats {
    pub uptime: u64,
    pub playtime: u64,
    pub artists: usize,
    pub albums: usize,
    pub songs: usize,
    pub db_playtime: u64,
    pub db_update: u64,
}

fn extract_cmd(cmd: &str) -> Result<(String, Vec<String>), MpdError> {
    let mut chars = cmd.trim().chars();

    let mut cmd = String::new();
    for ch in chars.by_ref() {
        if ch.is_ascii_whitespace() {
            break;
        } else {
            cmd.push(ch);
        }
    }

    let mut args = Vec::new();
    let mut current_arg = String::new();

    'outer: while let Some(ch) = chars.next() {
        if ch == '\"' {
            for ch in chars.by_ref() {
                if ch == '\"' {
                    args.push(current_arg.clone());
                    current_arg.clear();
                    continue 'outer;
                } else {
                    current_arg.push(ch);
                }
            }
            return Err(MpdError::new(
                Some(cmd),
                "missing closing '\"'".to_string(),
                Ack::Unknown,
            ));
        } else if ch.is_ascii_whitespace() {
            if !current_arg.is_empty() {
                args.push(current_arg.clone());
                current_arg.clear();
            }
        } else {
            current_arg.push(ch);
        }
    }
    if !current_arg.is_empty() {
        args.push(current_arg);
    }

    Ok((cmd, args))
}

fn parse_bool(text: &str, cmd: &str) -> Result<bool, MpdError> {
    match text {
        "1" => Ok(true),
        "0" => Ok(false),
        _ => Err(MpdError::boolean_expected(cmd)),
    }
}

fn tag_from_args<I: Iterator<Item = String>>(args: &mut I, cmd: &str) -> Result<TagType, MpdError> {
    let tag = args
        .next()
        .ok_or_else(|| MpdError::wrong_argument_count(cmd))?;
    TagType::from_str(&tag).map_err(|_| {
        MpdError::new(
            Some(cmd.to_string()),
            format!("unknown tag type: {}", tag),
            Ack::Arg,
        )
    })
}

#[derive(Serialize)]
pub struct ReflectionCommand {
    pub command: String,
}

#[derive(PartialEq, Eq, Debug)]
pub struct Filter {
    pub tag: TagType,
    pub expression: String,
}

fn parse_position(pos_str: &str, cmd: &str) -> Result<Position, MpdError> {
    Ok(if let Some(after_current) = pos_str.strip_prefix('+') {
        Position::After(
            after_current
                .parse()
                .map_err(|_| MpdError::integer_expeted(cmd))?,
        )
    } else if let Some(before_current) = pos_str.strip_prefix('-') {
        Position::Before(
            before_current
                .parse()
                .map_err(|_| MpdError::integer_expeted(cmd))?,
        )
    } else {
        Position::Absolute(
            pos_str
                .parse()
                .map_err(|_| MpdError::integer_expeted(cmd))?,
        )
    })
}

fn parse_filters<I: Iterator<Item = String>>(
    args: &mut I,
    cmd: &str,
) -> Result<Vec<Filter>, MpdError> {
    let mut filters = vec![];
    while let Some(tag_str) = args.next() {
        let tag = TagType::from_str(&tag_str).map_err(|_| {
            MpdError::new(
                Some(cmd.to_string()),
                format!("unknown tag type: {}", tag_str),
                Ack::Arg,
            )
        })?;
        let expression = args.next().ok_or_else(|| {
            MpdError::new(
                Some(cmd.to_string()),
                "missing filter expression".to_string(),
                Ack::Arg,
            )
        })?;
        // TODO is this needed?
        if !expression.is_empty() {
            filters.push(Filter { tag, expression })
        }
    }
    Ok(filters)
}

#[cfg(test)]
mod tests {
    use std::str::FromStr;

    use super::Command;
    use crate::mpd::model::{Filter, TagType, TagTypeCommand};

    #[test]
    fn commands() {
        // assert_eq!()
    }

    #[test]
    fn basic_cmd() {
        assert_eq!(Command::from_str("status").unwrap(), Command::Status)
    }

    #[test]
    fn cmd_extract_quotes() {
        assert_eq!(
            Command::from_str("lsinfo \"mydir/my artist\"").unwrap(),
            Command::LsInfo(Some("mydir/my artist".to_string()))
        )
    }

    #[test]
    fn cmd_extract_missing_quote() {
        assert!(Command::from_str("mycommand \"12 34 56").is_err(),)
    }

    #[test]
    fn cmd_extract_empty_arg() {
        assert_eq!(
            Command::from_str("find Artist \"\"").unwrap(),
            Command::Find(vec![Filter {
                tag: TagType::Artist,
                expression: "".to_string()
            }])
        )
    }

    #[test]
    fn cmd_bool() {
        assert_eq!(
            Command::from_str("repeat \"1\"").unwrap(),
            Command::Repeat(true)
        )
    }

    #[test]
    fn tag_type_enable() {
        assert_eq!(
            Command::from_str("tagtypes enable Artist").unwrap(),
            Command::TagTypes(TagTypeCommand::Enable(TagType::Artist))
        )
    }

    #[test]
    fn list_with_filter() {
        assert_eq!(
            Command::from_str("list Album Artist artist1").unwrap(),
            Command::List(
                TagType::Album,
                vec![Filter {
                    tag: TagType::Artist,
                    expression: "artist1".to_string()
                }]
            )
        )
    }

    #[test]
    fn empty_find() {
        assert_eq!(Command::from_str("find").unwrap(), Command::Find(vec![]),)
    }

    #[test]
    fn find() {
        assert_eq!(
            Command::from_str("find Artist artist1 Album album1").unwrap(),
            Command::Find(vec![
                Filter {
                    tag: TagType::Artist,
                    expression: "artist1".to_string()
                },
                Filter {
                    tag: TagType::Album,
                    expression: "album1".to_string()
                }
            ]),
        )
    }
}
