use gstreamer_player::PlayerState;
use indexmap::IndexMap;
use log::{error, info};
use serde::{Deserialize, Serialize};
use std::{fs, path::PathBuf, sync::Arc};

#[derive(Serialize, Deserialize, Debug, Default)]
pub struct State {
    queue: IndexMap<u64, Arc<str>>,
    next_id: u64,
    current_pos: Option<usize>,
    playlist_version: u64,
    playback_state: Option<u8>,
    pub(super) media_position: u64,
    pub(super) volume: Option<f64>,
}

impl State {
    pub fn queue(&self) -> &IndexMap<u64, Arc<str>> {
        &self.queue
    }

    pub fn current_item_id(&self) -> Option<&str> {
        self.current_pos
            .map(|pos| self.queue.get_index(pos).unwrap().1.as_ref())
    }

    pub fn current_pos(&self) -> Option<usize> {
        self.current_pos
    }

    pub fn playlist_version(&self) -> u64 {
        self.playlist_version
    }

    /// Returns the URL of the item set as current
    pub fn set_current(&mut self, pos: usize) -> Option<&str> {
        if let Some((_, item)) = self.queue.get_index(pos) {
            self.current_pos = Some(pos);
            Some(item)
        } else {
            None
        }
    }

    pub fn set_current_by_id(&mut self, id: u64) -> Option<&str> {
        if let Some((pos, _, item)) = self.queue.get_full(&id) {
            self.current_pos = Some(pos);
            Some(item)
        } else {
            None
        }
    }

    pub fn add_item(&mut self, item_id: Arc<str>) -> u64 {
        self.playlist_version += 1;

        let id = self.next_id;
        self.queue.insert(id, item_id);
        self.next_id += 1;
        id
    }

    pub fn move_next(&mut self) -> Option<&str> {
        self.current_pos.and_then(|current| {
            let next = current + 1;
            self.queue.get_index(next).map(|(_, item)| {
                self.current_pos = Some(next);
                item.as_ref()
            })
        })
    }

    pub fn move_previous(&mut self) -> Option<&str> {
        self.current_pos
            .filter(|current| *current != 0)
            .and_then(|current| {
                let prev = current - 1;
                self.queue.get_index(prev).map(|(_, item)| {
                    self.current_pos = Some(prev);
                    item.as_ref()
                })
            })
    }

    pub fn clear(&mut self) {
        self.current_pos = None;
        self.queue.clear();
        self.playlist_version += 1;
    }

    pub fn save(&self) {
        let path = state_path();
        fs::create_dir_all(path.parent().unwrap()).expect("Could not create state dir");
        let contents = serde_json::to_string(&self).unwrap();
        if let Err(err) = fs::write(path, contents) {
            error!("Could not save state: {err}");
        }
    }

    pub fn load() -> Option<Self> {
        let path = state_path();
        if path.exists() {
            match fs::read_to_string(path) {
                Ok(contents) => match serde_json::from_str::<Self>(&contents) {
                    Ok(state) => {
                        info!("Loaded state with {} songs in queue", state.queue.len());
                        Some(state)
                    }
                    Err(err) => {
                        error!("Could not deserialize state: {err}");
                        None
                    }
                },
                Err(err) => {
                    error!("Could not read state file: {err}");
                    None
                }
            }
        } else {
            None
        }
    }

    pub fn playback_state(&self) -> Option<PlayerState> {
        self.playback_state.and_then(deserialize_player_state)
    }

    pub fn set_playback_state(&mut self, value: PlayerState) {
        self.playback_state = Some(serialize_player_state(value));
    }
}

fn state_path() -> PathBuf {
    dirs::data_dir()
        .expect("Config dir should be avilable")
        .join("mpdfin")
        .join("state.json")
}

fn serialize_player_state(state: PlayerState) -> u8 {
    match state {
        PlayerState::Stopped => 0,
        PlayerState::Buffering => 1,
        PlayerState::Paused => 2,
        PlayerState::Playing => 3,
        _ => 4,
    }
}

fn deserialize_player_state(value: u8) -> Option<PlayerState> {
    match value {
        0 => Some(PlayerState::Stopped),
        1 => Some(PlayerState::Buffering),
        2 => Some(PlayerState::Paused),
        3 => Some(PlayerState::Playing),
        _ => None,
    }
}
