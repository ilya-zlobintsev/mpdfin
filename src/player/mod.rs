mod state;

pub use self::state::State;

use crate::{
    database::Database,
    jellyfin::JellyfinClient,
    mpd::{subsystem::SubsystemNotifier, Subsystem},
};
use log::debug;
use std::{
    cell::{Ref, RefCell},
    rc::Rc,
};
use vlc::{EventType, Instance, Media, MediaPlayer, MediaPlayerAudioEx};

pub struct Player {
    instance: Instance,
    media_player: Rc<MediaPlayer>,

    subsystem_notifier: SubsystemNotifier,
    state: Rc<RefCell<State>>,

    database: Rc<RefCell<Database>>,
    jellyfin_client: JellyfinClient,
}

impl Player {
    pub fn new(
        subsystem_notifier: SubsystemNotifier,
        database: Rc<RefCell<Database>>,
        jellyfin_client: JellyfinClient,
        state: State,
        // ex: &LocalExecutor,
    ) -> Self {
        let state = Rc::new(RefCell::new(state));

        let instance = Instance::new().expect("Could not initialize instance");

        let user_agent = env!("CARGO_PKG_NAME");
        instance.set_user_agent(user_agent, user_agent);

        let media_player = MediaPlayer::new(&instance).expect("Could not initialize media player");

        let event_manager = media_player.event_manager();

        for event_type in [
            EventType::MediaPlayerPlaying,
            EventType::MediaPlayerStopped,
            EventType::MediaPlayerPaused,
            EventType::MediaPlayerEndReached,
            EventType::MediaStateChanged,
        ] {
            let subsystem_notifier = subsystem_notifier.clone();

            event_manager
                .attach(event_type, move |_, _| {
                    subsystem_notifier.notify(Subsystem::Player);
                    subsystem_notifier.notify(Subsystem::Mixer);
                })
                .unwrap();
        }

        Self {
            instance,
            media_player: Rc::new(media_player),
            subsystem_notifier,
            database,
            state,
            jellyfin_client,
        }
    }

    pub fn play_by_id(&self, queue_id: u64) {
        self.state.borrow_mut().set_current_by_id(queue_id);
        self.play_current();
    }

    pub fn play_by_pos(&self, pos: usize) {
        self.state.borrow_mut().set_current(pos);
        self.play_current();
    }

    fn play_current(&self) {
        if let Some(item_id) = self.state.borrow().current_item_id() {
            let url = &self.jellyfin_client.get_audio_stream_url(item_id);
            debug!("creating media at new url");
            let media = Media::new_location(&self.instance, url).expect("Invalid URL");
            self.media_player.set_media(&media);
            debug!("starting playback");
            self.media_player.play().unwrap();
            debug!("started playback");
        } else {
            self.media_player.stop();
        }

        self.subsystem_notifier.notify(Subsystem::Player);
    }

    pub fn is_playing(&self) -> bool {
        self.media_player.is_playing()
    }

    pub fn play(&self) {
        self.media_player.play().unwrap();
    }

    pub fn pause(&self) {
        self.media_player.pause();
    }

    pub fn next(&self) {
        self.state.borrow_mut().move_next();
        self.play_current();
    }

    pub fn previous(&self) {
        self.state.borrow_mut().move_previous();
        self.play_current();
    }

    pub fn toggle(&self) {
        if self.media_player.is_playing() {
            self.media_player.pause();
        } else {
            let _ = self.media_player.play();
        }
        self.subsystem_notifier.notify(Subsystem::Player);
    }

    pub fn stop(&self) {
        self.media_player.stop();
        self.subsystem_notifier.notify(Subsystem::Player);
    }

    pub fn set_volume(&self, volume: i32) {
        self.media_player.set_volume(volume).unwrap();
        self.subsystem_notifier.notify(Subsystem::Mixer);
    }

    pub fn volume(&self) -> i32 {
        self.media_player.get_volume()
    }

    pub fn state(&self) -> Ref<'_, State> {
        self.state.borrow()
    }

    pub fn add_item(&self, item_id: Rc<str>) -> Option<u64> {
        if !self.database.borrow().items.contains_key(&item_id) {
            return None;
        }

        let id = self.state.borrow_mut().add_item(item_id);
        self.subsystem_notifier.notify(Subsystem::Playlist);
        Some(id)
    }

    pub fn clear(&self) {
        let mut state = self.state.borrow_mut();
        state.clear();
        self.subsystem_notifier.notify(Subsystem::Playlist);

        self.stop();
    }

    pub fn playback_state(&self) -> vlc::State {
        self.state()
            .playback_state
            .map(vlc::State::from)
            .unwrap_or(vlc::State::Stopped)
    }

    /// In ms
    pub fn media_length(&self) -> Option<i64> {
        self.media_player
            .get_media()
            .and_then(|media| media.duration())
    }

    /// Returns value between 0 and 1
    pub fn media_position(&self) -> Option<f32> {
        self.media_player.get_position()
    }

    fn update_playback_state(&self) {
        self.state.borrow_mut().playback_state = Some(self.media_player.state() as u32);
    }

    // fn send_event(&mut self, event: PlayerEvent) {
    //     let _ = self.event_sender.send(event);
    // }

    // pub fn subscribe(&self) -> broadcast::Receiver<PlayerEvent> {
    //     self.event_sender.subscribe()
    // }
}
