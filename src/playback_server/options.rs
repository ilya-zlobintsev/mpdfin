use std::sync::{Arc, RwLock};

use crate::mpd::model::{State, Subsystem};

use super::PlaybackServer;

type Shared<T> = Arc<RwLock<T>>;

#[derive(Clone, Default, Debug)]
pub(super) struct PlaybackOptions {
    pub repeat: Shared<bool>,
    pub random: Shared<bool>,
    pub single: Shared<bool>,
    pub consume: Shared<bool>,
    pub state: Shared<State>,
}

impl PlaybackServer {
    pub fn set_repeat(&self, value: bool) {
        self.update_tx
            .send(Subsystem::Options)
            .expect("Failed to send subsystem update");
        *self.options.repeat.write().unwrap() = value
    }

    pub fn repeat(&self) -> bool {
        *self.options.repeat.read().unwrap()
    }

    pub fn set_random(&self, value: bool) {
        self.update_tx
            .send(Subsystem::Options)
            .expect("Failed to send subsystem update");
        *self.options.random.write().unwrap() = value
    }

    pub fn random(&self) -> bool {
        *self.options.random.read().unwrap()
    }

    pub fn set_single(&self, value: bool) {
        self.update_tx
            .send(Subsystem::Options)
            .expect("Failed to send subsystem update");
        *self.options.single.write().unwrap() = value
    }
    pub fn single(&self) -> bool {
        *self.options.single.read().unwrap()
    }

    pub fn set_consume(&self, value: bool) {
        self.update_tx
            .send(Subsystem::Options)
            .expect("Failed to send subsystem update");
        *self.options.consume.write().unwrap() = value
    }

    pub fn consume(&self) -> bool {
        *self.options.consume.read().unwrap()
    }

    pub fn set_state(&self, value: State) {
        *self.options.state.write().unwrap() = value;
    }

    pub fn state(&self) -> State {
        *self.options.state.read().unwrap()
    }
}
