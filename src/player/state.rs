use indexmap::IndexMap;
use serde::{Deserialize, Serialize};
use std::rc::Rc;

use crate::{database::Database, jellyfin::base::BaseItemDto};

#[derive(Serialize, Deserialize, Debug, Default)]
pub struct State {
    pub queue: IndexMap<usize, QueueItem>,
    next_id: usize,
}

impl State {
    pub fn add_item(&mut self, item_id: Rc<str>) -> usize {
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
