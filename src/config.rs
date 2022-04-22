use serde::{Deserialize, Serialize};
use std::path::PathBuf;

use crate::database::storage::{Storage, StorageFormat};

#[derive(Serialize, Deserialize, Debug, Default)]
#[serde(default)]
pub struct Config {
    pub jellyfin: Jellyfin,
    pub mpd: Mpd,
    pub player: Player,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct Jellyfin {
    pub url: String,
    pub username: String,
    pub password: String,
}

#[derive(Serialize, Deserialize, Debug)]
#[serde(default)]
pub struct Mpd {
    pub address: String,
    pub port: u32,
}

#[derive(Serialize, Deserialize, Debug)]
#[serde(default)]
pub struct Player {
    pub mpris: bool,
}

impl Storage for Config {
    fn get_format() -> StorageFormat {
        StorageFormat::Toml
    }

    fn get_path() -> PathBuf {
        dirs::config_dir()
            .expect("Cannot find config dir")
            .join("mpdfin")
            .join("config.toml")
    }
}

impl Config {
    pub async fn load_or_create() -> anyhow::Result<Self> {
        Ok(match Config::load().await? {
            Some(config) => config,
            None => {
                let config = Config::default();
                config.save().await?;
                println!(
                    "New config created! Please edit {}",
                    Self::get_path().into_os_string().into_string().unwrap()
                );
                std::process::exit(0);
            }
        })
    }
}

impl Default for Jellyfin {
    fn default() -> Self {
        Self {
            url: "https://jellyfin.example.com".to_string(),
            username: "user".to_string(),
            password: "password123".to_string(),
        }
    }
}

impl Default for Mpd {
    fn default() -> Self {
        Self {
            address: "127.0.0.1".to_string(),
            port: 6600,
        }
    }
}

impl Default for Player {
    fn default() -> Self {
        Self { mpris: true }
    }
}
