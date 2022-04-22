use std::path::PathBuf;

use tokio::{
    fs::{create_dir_all, File},
    io::AsyncWriteExt,
};

use super::storage::data_dir;

#[derive(Clone)]
pub struct MusicStorage {
    dir: PathBuf,
}

impl MusicStorage {
    pub async fn new() -> Self {
        let dir = data_dir().join("cache");
        if !dir.is_dir() {
            create_dir_all(dir)
                .await
                .expect("Failed to create cache dir");
        }
        Self {
            dir: data_dir().join("cache"),
        }
    }

    pub fn get_item(&self, id: &str) -> Option<PathBuf> {
        let path = self.dir.join(id);
        if path.is_file() {
            Some(path)
        } else {
            None
        }
    }

    pub async fn save(&self, id: &str, contents: &[u8]) -> tokio::io::Result<PathBuf> {
        let path = self.dir.join(id);
        let mut file = File::create(&path).await?;
        file.write_all(contents).await?;
        Ok(path)
    }
}
