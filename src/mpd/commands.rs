use std::{collections::BTreeSet, str::FromStr, time::SystemTime};
use strum::VariantNames;
use tokio::{
    io::{AsyncBufReadExt, AsyncWriteExt, BufReader},
    net::TcpStream,
};
use tracing::{debug_span, Instrument};

use super::{
    error::{Ack, MpdError},
    filters::{album_matches, artist_matches, song_matches},
    model::{
        AddId, ChangedEvent, DirectoryEntry, Filter, ListEntry, PlaylistInfo, SongEntry, Stats,
        Subsystem, TagType, TagTypeCommand, Update,
    },
    MpdServer,
};
use crate::{
    mpd::{model::Command, serializer},
    playback_server::queue::Position,
};

type Result<T> = std::result::Result<T, MpdError>;

impl MpdServer {
    pub async fn process_mpd_command(
        &self,
        cmd: &str,
        stream: &mut BufReader<TcpStream>,
        tag_types: &mut BTreeSet<TagType>,
    ) -> Result<String> {
        if cmd.is_empty() {
            return Err(MpdError::new(
                None,
                "empty string as command".to_string(),
                Ack::Unknown,
            ));
        }

        Ok(match Command::from_str(cmd)? {
            Command::Status => serializer::to_string(&self.playback_server.status())?,
            Command::CurrentSong => {
                serializer::to_string(&self.playback_server.queue.get_current_item())?
            }
            Command::PlChanges => serializer::to_string(&*self.playback_server.queue.get_list())?,
            Command::TagTypes(tag_cmd) => match tag_cmd {
                TagTypeCommand::List => {
                    let tag_types: Vec<String> = tag_types
                        .iter()
                        .map(|tag_type| tag_type.to_string())
                        .collect();
                    serializer::to_string(&("tagtype", tag_types))?
                }
                TagTypeCommand::Enable(_) => todo!(),
                TagTypeCommand::Disable(_) => todo!(),
                TagTypeCommand::Clear => todo!(),
                TagTypeCommand::All => todo!(),
            },
            Command::LsInfo(path) => self.lsinfo(path.as_deref()).await?,
            Command::Repeat(repeat) => {
                self.playback_server.set_repeat(repeat);
                String::new()
            }
            Command::Random(random) => {
                self.playback_server.set_random(random);
                String::new()
            }
            Command::Single(single) => {
                self.playback_server.set_single(single);
                String::new()
            }
            Command::Consume(consume) => {
                self.playback_server.set_consume(consume);
                String::new()
            }
            Command::PlaylistInfo(info) => {
                let list = self.playback_server.queue.get_list();

                let results = match info {
                    Some(PlaylistInfo::SongPos(pos)) => vec![list
                        .get(pos)
                        .ok_or_else(|| {
                            MpdError::new(
                                Some(cmd.to_string()),
                                format!("song with index {} not found", pos),
                                Ack::Arg,
                            )
                        })?
                        .clone()],
                    Some(PlaylistInfo::StartEnd(start, end)) => list[start..end].to_vec(),
                    None => list.clone(),
                };

                serializer::to_string(&results)?
            }
            Command::Playlist => serializer::to_string(&*self.playback_server.queue.get_list())?, // TODO format
            Command::Ping => String::new(),
            Command::Idle(filters) => serializer::to_string(&self.idle(filters, stream).await)?,
            Command::NoIdle => String::new(),
            Command::Outputs => serializer::to_string(&self.playback_server.get_outputs())?,
            Command::Decoders => serializer::to_string(&self.playback_server.get_decoders())?,
            Command::Update => {
                let db = self.db.clone();
                tokio::spawn(async move {
                    if let Err(e) = db.update().instrument(debug_span!("Updating DB")).await {
                        tracing::error!("Failed to update DB: {}", e);
                    }
                });

                serializer::to_string(&Update {
                    updating_db: 1, // TODO
                })?
            }
            Command::Add(item_id, pos) => {
                self.add(&item_id, pos).await?;
                String::new()
            }
            Command::AddId(item_id, pos) => {
                let id = self.add(&item_id, pos).await?;
                serializer::to_string(&AddId { id })?
            }
            Command::List(tag_type, filters) => self.list(&tag_type, filters).await?,
            Command::ListPlaylists => {
                let storage = self.db.get_storage().await;
                let playlists: Vec<String> = storage
                    .playlists
                    .iter()
                    .map(|item| item.name.clone())
                    .collect();

                serializer::to_string(&("playlist", playlists))?
            }
            Command::ListPlaylistInfo(playlist_name) => {
                serializer::to_string(&self.db.get_playlist_items(&playlist_name).await.map_err(
                    |e| {
                        tracing::info!("{}", e);
                        MpdError::new(
                            Some("listplaylistinfo".to_string()),
                            "no such playlist".to_string(),
                            Ack::NotFound,
                        )
                    },
                )?)?
            }
            Command::Commands => {
                let commands = Command::VARIANTS;
                serializer::to_string(&("command", commands))?
            }
            Command::Play => {
                // TODO id
                self.playback_server.play();
                String::new()
            }
            Command::PlayId(maybe_index) => {
                if let Some(index) = maybe_index {
                    self.playback_server.queue.navigate_to(index)?
                }
                self.playback_server.play();
                String::new()
            }
            Command::Pause(pause) => {
                match pause {
                    Some(true) => self.playback_server.pause(),
                    Some(false) => self.playback_server.play(),
                    None => self.playback_server.toggle_playback(),
                }
                String::new()
            }
            Command::Stats => serializer::to_string(&self.stats().await)?,
            Command::UrlHandlers => String::new(),
            Command::Find(filters) => serializer::to_string(&self.search(filters, false).await)?,
            Command::Search(filters) => serializer::to_string(&self.search(filters, true).await)?,
            Command::Volume(vol) => {
                self.volume(vol);
                String::new()
            }
            Command::SetVol(vol) => {
                self.setvol(vol)?;
                String::new()
            }
        })
    }

    pub async fn lsinfo(&self, path: Option<&str>) -> Result<String> {
        let storage = self.db.get_storage().await;
        let items = match path {
            None | Some("") => storage
                .artists
                .iter()
                .map(|artist| {
                    ListEntry::Directory(DirectoryEntry {
                        directory: artist.name.clone(),
                    })
                })
                .collect::<Vec<ListEntry>>(),
            Some(path) => {
                let split: Vec<&str> = path.split("/").collect();
                match split.len() {
                    1 => storage
                        .albums
                        .iter()
                        .filter(|album| album.album_artist.as_deref() == Some(path))
                        .map(|album| {
                            // if let Some(premiere_date) = album.premiere_date {
                            //     dir.push_str(" (");
                            //     dir.push_str(&premiere_date.year().to_string());
                            //     dir.push(')');
                            // }
                            ListEntry::Directory(DirectoryEntry {
                                directory: format!("{}/{}", path, album.name),
                            })
                        })
                        .collect::<Vec<ListEntry>>(),
                    2 => {
                        let artist_name = *split.first().unwrap();
                        let album_name = *split.last().unwrap();

                        storage
                            .songs
                            .iter()
                            .filter(|song| {
                                song.artists
                                    .iter()
                                    .filter(|artist| *artist == artist_name)
                                    .next()
                                    .is_some()
                            })
                            .filter(|song| song.album.as_deref() == Some(album_name))
                            .map(|song| ListEntry::File(SongEntry::from(song.clone())))
                            .collect::<Vec<ListEntry>>()
                    }
                    _ => {
                        return Err(MpdError::new(
                            Some("lsinfo".to_string()),
                            "Invalid path format".to_string(),
                            Ack::NotFound,
                        ))
                    }
                }
            }
        };
        if !items.is_empty() {
            Ok(serializer::to_string(&items)?)
        } else {
            Err(MpdError::new(
                Some("lsinfo".to_string()),
                "no such directory".to_string(),
                Ack::NotFound,
            ))
        }
    }

    async fn idle(
        &self,
        filters: Vec<Subsystem>,
        stream: &mut BufReader<TcpStream>,
    ) -> Option<ChangedEvent> {
        let mut event_rx = self.update_tx.subscribe();
        tokio::select! {
            Some(changed) = async {
                while let Ok(changed) = event_rx.recv().await {
                    if filters.is_empty() {
                        return Some(changed);
                    } else {
                        if filters.contains(&changed) {
                            return Some(changed)
                        }
                    }
                }
                None
             } => Some(ChangedEvent{ changed }),
            _ = async {
                let mut buf = String::new();
                while stream.read_line(&mut buf).await.unwrap() != 0{
                    if buf.trim() == "noidle" {
                        tracing::debug!("exited idle");
                        break;
                    } {
                        stream.shutdown().await.expect("Failed to shut the stream down");
                    }
                }
            } => None,
        }
    }

    async fn add(&self, song_id: &str, pos: Option<Position>) -> Result<usize> {
        match self
            .db
            .get_storage()
            .await
            .songs
            .iter()
            .find(|audio| &audio.id == song_id)
        {
            Some(audio) => {
                let file_entry = SongEntry::from(audio.clone());

                Ok(self.playback_server.queue.add(file_entry, pos))
            }
            None => Err(MpdError::new(
                None,
                "item not found".to_string(),
                Ack::NotFound,
            )),
        }
    }

    async fn list(&self, tag_type: &TagType, filters: Vec<Filter>) -> Result<String> {
        let storage_guard = self.db.get_storage().await;

        let names: Vec<&String> = match tag_type {
            TagType::Artist | TagType::ArtistSort => storage_guard
                .artists
                .iter()
                .filter(|song| artist_matches(song, &filters, false))
                .map(|artist| &artist.name)
                .collect(),
            TagType::Album => storage_guard
                .albums
                .iter()
                .filter(|album| album_matches(album, &filters, false))
                .map(|album| &album.name)
                .collect(),
            TagType::AlbumArtist | TagType::AlbumArtistSort => todo!(),
            TagType::Title => storage_guard
                .songs
                .iter()
                .filter(|song| song_matches(song, &filters, false))
                .map(|song| &song.name)
                .collect(),
            TagType::Track => todo!(),
            TagType::Name => todo!(),
            TagType::Genre => todo!(),
            TagType::Date => todo!(),
            TagType::Composer => todo!(),
            TagType::Performer => todo!(),
        };
        Ok(serializer::to_string(&(tag_type, names))?)
    }

    async fn stats(&self) -> Stats {
        let storage = self.db.get_storage().await;

        Stats {
            uptime: self.startup_instant.elapsed().as_secs(),
            playtime: 0, // TODO
            artists: storage.artists.len(),
            albums: storage.albums.len(),
            songs: storage.songs.len(),
            db_playtime: 0, // TODO
            db_update: self
                .db
                .last_update()
                .await
                .duration_since(SystemTime::UNIX_EPOCH)
                .expect("Failed to measure duration")
                .as_secs(),
        }
    }

    fn volume(&self, vol: i64) {
        let current_vol = self.playback_server.get_volume();

        self.playback_server.set_volume(current_vol + vol);
    }

    fn setvol(&self, vol: i64) -> Result<()> {
        if vol < 0 {
            Err(MpdError::new(
                Some("setvol".to_owned()),
                "number too small".to_owned(),
                Ack::Arg,
            ))
        } else if vol > 1000 {
            Err(MpdError::new(
                Some("setvol".to_owned()),
                "number too big".to_owned(),
                Ack::Arg,
            ))
        } else {
            self.playback_server.set_volume(vol);
            Ok(())
        }
    }

    async fn search(&self, filters: Vec<Filter>, ignore_case: bool) -> Vec<ListEntry> {
        let storage_guard = self.db.get_storage().await;

        let mut results = vec![];

        results.extend(storage_guard.artists.iter().filter_map(|artist| {
            if artist_matches(artist, &filters, ignore_case) {
                Some(ListEntry::Directory(DirectoryEntry {
                    directory: artist.name.clone(),
                }))
            } else {
                None
            }
        }));

        results.extend(storage_guard.albums.iter().filter_map(|album| {
            if album_matches(album, &filters, ignore_case) {
                Some(ListEntry::Directory(DirectoryEntry {
                    directory: format!(
                        "{}/{}",
                        album.album_artist.as_deref().unwrap_or(""),
                        album.name
                    ),
                }))
            } else {
                None
            }
        }));

        results.extend(storage_guard.songs.iter().filter_map(|song| {
            if song_matches(song, &filters, ignore_case) {
                Some(ListEntry::File(SongEntry::from(song.clone())))
            } else {
                None
            }
        }));

        results
    }
}
