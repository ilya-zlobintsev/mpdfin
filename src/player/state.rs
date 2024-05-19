use indexmap::IndexMap;
use serde::{Deserialize, Serialize};
use std::rc::Rc;

#[derive(Serialize, Deserialize, Debug, Default)]
pub struct State {
    pub queue: IndexMap<u64, QueueItem>,
    next_id: u64,
    pub current_pos: Option<usize>,
    pub playlist_version: u64,
}

impl State {
    pub fn add_item(&mut self, item_id: Rc<str>) -> u64 {
        self.playlist_version += 1;

        let id = self.next_id;
        self.queue.insert(id, QueueItem { item_id });
        self.next_id += 1;
        id
    }

    pub fn current_item_id(&self) -> Option<&str> {
        self.current_pos
            .map(|pos| self.queue.get_index(pos).unwrap().1.item_id.as_ref())
    }
}

#[derive(Serialize, Deserialize, Debug)]
pub struct QueueItem {
    pub item_id: Rc<str>,
}
