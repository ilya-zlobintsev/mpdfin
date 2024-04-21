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

    // pub fn items<'a>(
    //     &'a self,
    //     db: &'a Database,
    // ) -> impl Iterator<Item = (&'a QueueItem, &'a BaseItemDto)> {

    //     self.queue.iter()
    // }
}

#[derive(Serialize, Deserialize, Debug)]
pub struct QueueItem {
    pub item_id: Rc<str>,
}
