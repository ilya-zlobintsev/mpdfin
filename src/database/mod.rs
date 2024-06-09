mod tree;

use self::tree::build_tree;
pub use self::tree::TreeNode;

use crate::jellyfin::{
    base::{BaseItemDto, BaseItemKind},
    items::ItemsQuery,
    user::UserDto,
    JellyfinClient,
};
use anyhow::Context;
use log::info;
use serde::{Deserialize, Serialize};
use std::{
    collections::HashMap,
    fs::{self, File},
    io::BufReader,
    path::PathBuf,
    sync::Arc,
};

#[derive(Serialize, Deserialize)]
pub struct Database {
    pub items: HashMap<Arc<str>, Arc<BaseItemDto>>,
    pub token: Arc<str>,
    #[serde(skip)]
    pub tree_root: TreeNode,
}

impl Database {
    pub fn new(jellyfin_client: &JellyfinClient, current_user: &UserDto) -> anyhow::Result<Self> {
        info!("Loading database");
        let mut items = HashMap::new();

        let views = jellyfin_client.get_user_views(&current_user.id)?;
        let music_collection_id = views
            .items
            .into_iter()
            .find(|item| item.collection_type.as_deref() == Some("music"))
            .map(|item| item.id)
            .context("No music collection found")?;

        info!("Found music collection {music_collection_id}");

        let mut start_index = 0;
        let mut total = None;

        while total.is_none() || total.is_some_and(|total| start_index < total) {
            let response = jellyfin_client.get_user_items(
                &current_user.id,
                ItemsQuery {
                    parent_id: Some(&music_collection_id),
                    start_index: Some(start_index),
                    limit: Some(1000),
                    include_item_types: Some(BaseItemKind::Audio),
                    recursive: true,
                },
            )?;

            start_index += response.items.len() as i32;
            total = Some(response.total_record_count);

            items.extend(
                response
                    .items
                    .into_iter()
                    .map(|item| (item.id.clone(), Arc::new(item))),
            );
        }
        items.shrink_to_fit();

        info!("Loaded {start_index} items");
        let tree_root = build_tree(items.values());

        let token = jellyfin_client
            .settings
            .token
            .clone()
            .expect("The token is present if items were already received")
            .into();

        Ok(Self {
            items,
            tree_root,
            token,
        })
    }

    pub fn populate_tree(&mut self) {
        self.tree_root = build_tree(self.items.values());
    }

    pub fn save(&self) -> anyhow::Result<()> {
        let path = Self::file_path()?;
        let data = serde_json::to_string(self).unwrap();
        std::fs::write(path, data).context("Could not write database file")?;
        info!("Saved database to file");
        Ok(())
    }

    pub fn load() -> anyhow::Result<Option<Self>> {
        let path = Self::file_path()?;
        if let Ok(file) = File::open(path) {
            let reader = BufReader::new(file);
            let mut db: Self =
                serde_json::from_reader(reader).context("Could not deserialize stored database")?;
            db.populate_tree();
            Ok(Some(db))
        } else {
            Ok(None)
        }
    }

    fn file_path() -> anyhow::Result<PathBuf> {
        let dir = dirs::data_local_dir()
            .context("Could not get data dir")?
            .join("mpdfin");
        if !dir.exists() {
            fs::create_dir_all(&dir).context("Could not create data dir")?;
        }

        Ok(dir.join("database.json"))
    }
}
