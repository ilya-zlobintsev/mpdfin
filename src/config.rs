use anyhow::Context;
use serde::Deserialize;
use std::{fs, path::PathBuf};

#[derive(Deserialize)]
pub struct Config {
    #[serde(default)]
    pub general: General,
    pub jellyfin: Jellyfin,
    #[serde(default)]
    pub database: Database,
}

#[derive(Deserialize)]
pub struct General {
    #[serde(default = "default_log_level")]
    pub log_level: String,
    #[serde(default = "default_listen_host")]
    pub listen_host: String,
}

impl Default for General {
    fn default() -> Self {
        Self {
            log_level: default_log_level(),
            listen_host: default_listen_host(),
        }
    }
}

#[derive(Deserialize)]
pub struct Jellyfin {
    pub url: String,
    pub username: String,
    pub password: String,
}

#[derive(Deserialize, Default)]
pub struct Database {
    pub path: Option<String>,
}

impl Config {
    pub fn load() -> anyhow::Result<Self> {
        let contents = fs::read_to_string(config_path()).context("Could not read config file")?;
        toml::from_str(&contents).context("Could not parse config file")
    }
}

fn config_path() -> PathBuf {
    dirs::config_dir()
        .expect("Config dir should be avilable")
        .join("mpdfin")
        .join("config.toml")
}

fn default_listen_host() -> String {
    "0.0.0.0:6600".to_owned()
}

fn default_log_level() -> String {
    "info".to_owned()
}
