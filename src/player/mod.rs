mod state;

pub use self::state::State;

use crate::{
    database::Database,
    jellyfin::JellyfinClient,
    mpd::{subsystem::SubsystemNotifier, Subsystem},
};
use glib::clone;
use gstreamer::{glib, ClockTime};
use gstreamer_player::{PlayerGMainContextSignalDispatcher, PlayerState, PlayerVideoRenderer};
use log::{debug, error, trace};
use std::{
    cell::RefCell,
    rc::Rc,
    sync::{Arc, RwLock, RwLockReadGuard},
};

pub struct Player {
    media_player: gstreamer_player::Player,

    subsystem_notifier: SubsystemNotifier,
    state: Arc<RwLock<State>>,

    database: Rc<RefCell<Database>>,
    jellyfin_client: JellyfinClient,
}

impl Player {
    pub fn new(
        subsystem_notifier: SubsystemNotifier,
        database: Rc<RefCell<Database>>,
        jellyfin_client: JellyfinClient,
        state: State,
    ) -> Self {
        gstreamer::init().expect("Failed to initialize gstreamer");

        let main_loop = glib::MainLoop::new(None, false);

        let state = Arc::new(RwLock::new(state));

        let dispatcher = PlayerGMainContextSignalDispatcher::new(None);
        let media_player =
            gstreamer_player::Player::new(None::<PlayerVideoRenderer>, Some(dispatcher));

        let mut config = media_player.config().clone();
        config.set_name("Mpdfin");
        config.set_user_agent("Mpdfin");
        media_player.set_config(config).unwrap();

        media_player.connect_state_changed(
            clone!(@strong subsystem_notifier, @strong state => move |_player, new_state| {
                state.write().unwrap().set_playback_state(new_state);

                subsystem_notifier.notify(Subsystem::Player);
                subsystem_notifier.notify(Subsystem::Mixer);
            }),
        );

        media_player.connect_volume_changed(clone!(@strong subsystem_notifier => move |_player| {
            subsystem_notifier.notify(Subsystem::Player);
            subsystem_notifier.notify(Subsystem::Mixer);
        }));

        media_player.connect_error(|_player, err| {
            error!("Player error: {err}");
        });

        std::thread::spawn(move || {
            main_loop.run();
        });

        let player = Self {
            media_player,
            subsystem_notifier,
            database,
            state,
            jellyfin_client,
        };

        if let Some(item_id) = player.state().current_item_id() {
            let url = &player.jellyfin_client.get_audio_stream_url(item_id);
            player.media_player.set_uri(Some(url));
        }

        if let Some(volume) = player.state().volume {
            player.media_player.set_volume(volume);
        }

        if let Some(player_state) = player.state().playback_state() {
            match player_state {
                PlayerState::Buffering | PlayerState::Playing => player.play(),
                PlayerState::Paused => player.pause(),
                _ => (),
            }
        }

        let position = player.state().media_position;
        if position != 0 {
            player.media_player.seek(ClockTime::from_mseconds(position));
        }

        player
    }

    pub fn play_by_id(&self, queue_id: u64) {
        self.state.write().unwrap().set_current_by_id(queue_id);
        self.play_current();
    }

    pub fn play_by_pos(&self, pos: usize) {
        self.state.write().unwrap().set_current(pos);
        self.play_current();
    }

    fn play_current(&self) {
        if let Some(item_id) = self.state.read().unwrap().current_item_id() {
            let url = &self.jellyfin_client.get_audio_stream_url(item_id);
            self.media_player.set_uri(Some(url));
            debug!("starting playback");
            self.media_player.play();
            debug!("started playback");
        } else {
            self.media_player.stop();
        }

        self.subsystem_notifier.notify(Subsystem::Player);
    }

    /// Time in seconds
    pub fn seek(&self, time: f64) {
        debug!("Seeking to {time}");
        self.media_player
            .seek(ClockTime::from_mseconds((time * 1000.0) as u64));
    }

    pub fn is_playing(&self) -> bool {
        self.state.read().unwrap().playback_state() == Some(PlayerState::Playing)
    }

    pub fn play(&self) {
        self.media_player.play();
    }

    pub fn pause(&self) {
        self.media_player.pause();
    }

    pub fn next(&self) {
        self.state.write().unwrap().move_next();
        self.play_current();
    }

    pub fn previous(&self) {
        self.state.write().unwrap().move_previous();
        self.play_current();
    }

    pub fn toggle(&self) {
        if self.is_playing() {
            self.media_player.pause();
        } else {
            self.media_player.play();
        }
        self.subsystem_notifier.notify(Subsystem::Player);
    }

    pub fn stop(&self) {
        self.media_player.stop();
        self.subsystem_notifier.notify(Subsystem::Player);
    }

    /// Takes value 0-100
    pub fn set_volume(&self, volume: i32) {
        let value = volume as f64 / 100.0;
        self.media_player.set_volume(value);
        self.subsystem_notifier.notify(Subsystem::Mixer);
    }

    pub fn volume(&self) -> i32 {
        (self.media_player.volume() * 100.0).round() as i32
    }

    pub fn state(&self) -> RwLockReadGuard<'_, State> {
        trace!("Acquiring state read lock");
        self.state.try_read().unwrap()
    }

    pub fn add_item(&self, item_id: Arc<str>) -> Option<u64> {
        if !self.database.borrow().items.contains_key(&item_id) {
            return None;
        }

        let id = self.state.write().unwrap().add_item(item_id);
        self.subsystem_notifier.notify(Subsystem::Playlist);
        Some(id)
    }

    pub fn clear(&self) {
        let mut state = self.state.write().unwrap();
        state.clear();
        self.subsystem_notifier.notify(Subsystem::Playlist);

        self.stop();
    }

    pub fn playback_state(&self) -> PlayerState {
        self.state()
            .playback_state()
            .unwrap_or(PlayerState::Stopped)
    }

    /// In ms
    pub fn media_length(&self) -> Option<u64> {
        self.media_player
            .media_info()
            .and_then(|info| info.duration())
            .map(|duration| duration.mseconds())
    }

    /// In ms
    pub fn media_position(&self) -> Option<u64> {
        self.media_player
            .position()
            .map(|position| position.mseconds())
    }

    pub fn save_state(&self) {
        let mut state = self.state.write().unwrap();
        state.media_position = self.media_position().unwrap_or(0);
        state.volume = Some(self.media_player.volume());
        state.save();

        debug!("Saved state");
    }
}
