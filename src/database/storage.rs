use anyhow::anyhow;
use async_trait::async_trait;
use jellyfin_client::model::{Audio, Item, MusicAlbum};
use serde::{de::DeserializeOwned, Deserialize, Serialize};
use std::{io::ErrorKind, path::PathBuf};

pub enum StorageFormat {
    Toml,
    Bincode,
}

#[async_trait]
pub trait Storage: Serialize + DeserializeOwned {
    fn get_path() -> PathBuf;

    fn get_format() -> StorageFormat;

    async fn load() -> anyhow::Result<Option<Self>> {
        match tokio::fs::read(Self::get_path()).await {
            Ok(bytes) => {
                let obj = match Self::get_format() {
                    StorageFormat::Toml => toml::from_slice(&bytes)?,
                    StorageFormat::Bincode => bincode::deserialize(&bytes)?,
                };

                Ok(Some(obj))
            }
            Err(e) => {
                if e.kind() == ErrorKind::NotFound {
                    Ok(None)
                } else {
                    Err(anyhow!(e))
                }
            }
        }
    }

    async fn save(&self) -> anyhow::Result<()> {
        let path = Self::get_path();
        tokio::fs::create_dir_all(path.parent().unwrap()).await?;

        let contents = match Self::get_format() {
            StorageFormat::Toml => toml::to_vec(self)?,
            StorageFormat::Bincode => bincode::serialize(self)?,
        };
        tokio::fs::write(path, contents).await?;

        Ok(())
    }
}

#[derive(Default, Serialize, Deserialize)]
pub struct DatabaseStorage {
    pub artists: Vec<Item>,
    pub albums: Vec<MusicAlbum>,
    pub songs: Vec<Audio>,
    pub playlists: Vec<Item>,
}

impl Storage for DatabaseStorage {
    fn get_format() -> StorageFormat {
        StorageFormat::Bincode
    }

    fn get_path() -> PathBuf {
        data_dir().join("db")
    }
}

pub fn data_dir() -> PathBuf {
    dirs::data_local_dir()
        .expect("Cannot find local data dir")
        .join("mpdfin")
}
