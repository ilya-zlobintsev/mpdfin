use std::sync::{
    atomic::{AtomicUsize, Ordering},
    Arc, RwLock, RwLockReadGuard,
};

use serde::Serialize;
use tokio::sync::broadcast;

use crate::mpd::{
    error::{Ack, MpdError},
    model::{Song, SongEntry, Subsystem},
};

#[derive(Clone)]
pub struct Queue {
    current_position: Arc<AtomicUsize>,
    inner: Arc<RwLock<Vec<QueueItem>>>,
    update_tx: broadcast::Sender<Subsystem>,
}

impl Queue {
    pub fn new(update_tx: broadcast::Sender<Subsystem>) -> Self {
        Self {
            current_position: Arc::new(AtomicUsize::new(0)),
            inner: Arc::new(RwLock::new(vec![])),
            update_tx,
        }
    }

    pub fn get_list(&self) -> RwLockReadGuard<Vec<QueueItem>> {
        self.inner.read().unwrap()
    }

    pub fn len(&self) -> usize {
        self.get_list().len()
    }

    pub fn is_empty(&self) -> bool {
        self.len() == 0
    }

    pub fn get_current_item(&self) -> Option<QueueItem> {
        let index = self.current_position.load(Ordering::Relaxed);

        self.inner.read().unwrap().get(index).cloned()
    }

    pub fn navigate_to(&self, index: usize) -> Result<(), MpdError> {
        let list = self.get_list();
        if list.get(index).is_some() {
            self.current_position.store(index, Ordering::Relaxed);
            Ok(())
            // TODO: actually play the song
        } else {
            Err(MpdError::new(
                None,
                "no such song".to_string(),
                Ack::NotFound,
            ))
        }
    }

    pub fn add(&self, song: SongEntry, pos: Option<Position>) -> usize {
        let mut queue = self.inner.write().unwrap();

        self.update_tx.send(Subsystem::Playlist).unwrap();

        let pos = match pos {
            None => queue.len().checked_sub(1).unwrap_or_default(),
            Some(pos) => {
                let pos = match pos {
                    Position::Absolute(pos) => pos as usize,
                    Position::After(_) => todo!(),
                    Position::Before(_) => todo!(),
                };
                pos
            }
        };
        tracing::trace!("Inserting item into the queue at position {}", pos);
        queue.insert(pos, QueueItem { song, id: pos, pos });
        pos
    }
}

#[derive(PartialEq, Eq, Debug)]
pub enum Position {
    Absolute(i32),
    After(i32),
    Before(i32),
}

#[derive(Serialize, Debug, Clone)]
#[serde(rename_all = "PascalCase")]
pub struct QueueItem {
    pub song: SongEntry,
    pub id: usize,
    pub pos: usize,
}
