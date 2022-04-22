use anyhow::anyhow;
use jellyfin_client::{
    model::{Audio, Item, ItemType, MusicAlbum},
    JellyfinApi,
};
use std::{path::PathBuf, sync::Arc, time::SystemTime};
use tokio::sync::{broadcast, OwnedRwLockReadGuard, RwLock};
use tracing::{debug_span, trace_span, Instrument};

use self::{
    auth::AuthStorage,
    music_storage::MusicStorage,
    storage::{DatabaseStorage, Storage},
};
use crate::{
    config::Config,
    mpd::model::{ListEntry, SongEntry, Subsystem},
};

mod auth;
mod music_storage;
pub mod storage;

#[derive(Clone)]
pub struct Database {
    jellyfin_api: JellyfinApi,
    storage: Arc<RwLock<DatabaseStorage>>,
    music_storage: MusicStorage,
    event_tx: broadcast::Sender<Subsystem>,
    last_update: Arc<RwLock<SystemTime>>,
}

impl Database {
    pub async fn with_config(
        config: &Config,
        event_tx: broadcast::Sender<Subsystem>,
    ) -> anyhow::Result<Self> {
        let jellyfin_api = load_jellyfin(config)
            .instrument(debug_span!("Initializing Jellyfin API"))
            .await?;

        let storage = match DatabaseStorage::load().await {
            Ok(Some(storage)) => storage,
            Ok(None) => {
                tracing::info!("Creating new database");
                DatabaseStorage::default()
            }
            Err(e) => {
                tracing::warn!("Failed to load local db: {}, creating new one", e);
                DatabaseStorage::default()
            }
        };

        let database = Self {
            jellyfin_api,
            storage: Arc::new(RwLock::new(storage)),
            music_storage: MusicStorage::new().await,
            event_tx,
            last_update: Arc::new(RwLock::new(SystemTime::now())),
        };

        {
            let database = database.clone();
            tokio::spawn(async move {
                match database.update().await {
                    Ok(()) => database
                        .storage
                        .read()
                        .await
                        .save()
                        .await
                        .expect("Failed to save DB"),
                    Err(e) => {
                        tracing::error!("Failed to update DB: {}", e);
                    }
                }
            });
        }

        Ok(database)
    }

    pub async fn update(&self) -> anyhow::Result<()> {
        let data = self
            .jellyfin_api
            .get_items(
                None,
                vec![
                    ItemType::MusicArtist(Item::default()),
                    ItemType::MusicAlbum(MusicAlbum::default()),
                    ItemType::Audio(Audio::default()),
                    ItemType::Playlist(Item::default()),
                ],
                true,
                100_000,
                vec![],
            )
            .instrument(debug_span!("Loading Jellyfin data"))
            .await?;

        let mut storage = self.storage.write().await;
        storage.artists.clear();
        storage.albums.clear();
        storage.songs.clear();
        storage.playlists.clear();

        for item in data.items {
            match item {
                ItemType::MusicArtist(artist) => storage.artists.push(artist),
                ItemType::MusicAlbum(album) => storage.albums.push(album),
                ItemType::Audio(song) => storage.songs.push(song),
                ItemType::Playlist(playlist) => storage.playlists.push(playlist),
            }
        }
        *self.last_update.write().await = SystemTime::now();
        self.event_tx.send(Subsystem::Database)?;
        Ok(())
    }

    pub async fn get_storage(&self) -> OwnedRwLockReadGuard<DatabaseStorage> {
        self.storage.clone().read_owned().await
    }

    // pub async fn get_artists(&self) -> OwnedRwLockReadGuard<DatabaseStorage, Vec<Item>> {
    //     OwnedRwLockReadGuard::map(self.get_storage().await, |storage| &storage.artists)
    // }
    //

    pub async fn get_playlist_items(&self, playlist_name: &str) -> anyhow::Result<Vec<ListEntry>> {
        let storage = self.storage.read().await;
        let playlist = storage
            .playlists
            .iter()
            .find(|playlist| playlist.name == playlist_name)
            .ok_or_else(|| anyhow!("playlist not found"))?;

        Ok(self
            .jellyfin_api
            .get_playlist_items(&playlist.id)
            .await?
            .into_iter()
            .map(|audio| ListEntry::File(SongEntry::from(audio)))
            .collect())
    }

    pub async fn get_audio_file(&self, item_id: &str) -> anyhow::Result<PathBuf> {
        match self.music_storage.get_item(item_id) {
            Some(path) => Ok(path),
            None => {
                let bytes = async {
                    let response = self.jellyfin_api.get_audio_stream(item_id).await?;
                    let bytes = response.bytes().await?;
                    Ok::<_, anyhow::Error>(bytes)
                }
                .instrument(trace_span!("Downloading audio", item_id))
                .await?;

                Ok(self.music_storage.save(item_id, &bytes).await?)
            }
        }
    }

    pub async fn last_update(&self) -> SystemTime {
        *self.last_update.read().await
    }
}

async fn load_jellyfin(config: &Config) -> anyhow::Result<JellyfinApi> {
    let maybe_auth_info = match AuthStorage::load().await.ok().flatten() {
        Some(auth) => {
            // Re-auth if any of the settings were changed
            if (auth.username == config.jellyfin.username)
                && (auth.password == config.jellyfin.password)
                && (auth.url == config.jellyfin.url)
            {
                Some(auth.auth_info)
            } else {
                None
            }
        }
        None => None,
    };
    Ok(match maybe_auth_info {
        Some(auth_info) => {
            tracing::debug!("Using existing Jellyfin token");
            JellyfinApi::new_with_auth_info(&config.jellyfin.url, auth_info)?
        }
        None => {
            tracing::debug!("Generating new Jellyfin token");
            let api = JellyfinApi::new_with_password(
                &config.jellyfin.url,
                &config.jellyfin.username,
                &config.jellyfin.password,
            )
            .await?;
            let auth_info = api.get_auto_info().expect("missing auth info");
            AuthStorage {
                url: config.jellyfin.url.clone(),
                username: config.jellyfin.username.clone(),
                password: config.jellyfin.password.clone(),
                auth_info: auth_info.clone(),
            }
            .save()
            .await?;

            api
        }
    })
}
