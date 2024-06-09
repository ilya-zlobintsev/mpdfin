use crate::jellyfin::base::BaseItemDto;
use std::{collections::BTreeMap, sync::Arc};

#[derive(Debug)]
pub enum TreeNode {
    Directory(BTreeMap<String, TreeNode>),
    // Item id
    File(Arc<str>),
}

impl Default for TreeNode {
    fn default() -> Self {
        Self::Directory(BTreeMap::new())
    }
}

pub fn build_tree<'a>(items: impl Iterator<Item = &'a Arc<BaseItemDto>>) -> TreeNode {
    let mut root_node = BTreeMap::new();

    for item in items {
        let name = match &item.name {
            Some(name) => name.replace('/', "+"),
            None => format!("<item {}>", item.id),
        };

        if item.artists.is_empty() {
            root_node.insert(name.clone(), TreeNode::File(item.id.clone()));
        } else {
            for artist in &item.artists {
                let artist_node: &mut TreeNode =
                    root_node.entry(artist.replace('/', "+")).or_default();
                match artist_node {
                    TreeNode::Directory(artist_items) => {
                        if let Some(album) = item.album.clone() {
                            let album_node =
                                artist_items.entry(album.replace('/', "+")).or_default();
                            match album_node {
                                TreeNode::Directory(album_items) => {
                                    album_items
                                        .insert(name.clone(), TreeNode::File(item.id.clone()));
                                }
                                TreeNode::File(_) => panic!("Invalid albums node"),
                            }
                        } else {
                            artist_items.insert(name.clone(), TreeNode::File(item.id.clone()));
                        }
                    }
                    _ => panic!("Invalid artists node"),
                }
            }
        }
    }

    TreeNode::Directory(root_node)
}

impl TreeNode {
    pub fn navigate<'a>(&'a self, url: &'a str) -> Option<&'a TreeNode> {
        let mut node = self;
        for part in url.split('/') {
            if !part.is_empty() {
                let TreeNode::Directory(dir) = node else {
                    return None;
                };

                let child_node = dir.get(part)?;
                node = child_node;
            }
        }
        Some(node)
    }

    pub fn item_ids(&self) -> Vec<Arc<str>> {
        match self {
            TreeNode::Directory(dir) => dir.values().flat_map(|node| node.item_ids()).collect(),
            TreeNode::File(id) => vec![id.clone()],
        }
    }
}
