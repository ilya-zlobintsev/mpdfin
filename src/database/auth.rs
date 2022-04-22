use super::storage::{data_dir, Storage, StorageFormat};
use jellyfin_client::AuthInfo;
use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize)]
pub struct AuthStorage {
    pub url: String,
    pub username: String,
    pub password: String,
    pub auth_info: AuthInfo,
}

impl Storage for AuthStorage {
    fn get_path() -> std::path::PathBuf {
        data_dir().join("auth.toml")
    }

    fn get_format() -> StorageFormat {
        StorageFormat::Toml
    }
}
