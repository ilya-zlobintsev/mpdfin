mod state;

use self::state::State;
use crate::{
    database::Database,
    mpd::{subsystem::SubsystemNotifier, Subsystem},
};
use std::{
    cell::{Ref, RefCell},
    rc::Rc,
};
use vlc::{EventType, Instance, Media, MediaPlayer, MediaPlayerAudioEx};

pub struct Player {
    instance: Instance,
    media_player: MediaPlayer,
    subsystem_notifier: SubsystemNotifier,
    state: Rc<RefCell<State>>,
    database: Rc<RefCell<Database>>,
}

impl Player {
    pub fn new(subsystem_notifier: SubsystemNotifier, database: Rc<RefCell<Database>>) -> Self {
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
            media_player,
            subsystem_notifier,
            database,
            state: Rc::default(),
        }
    }

    pub fn play_url(&self, url: &str) {
        let media = Media::new_location(&self.instance, url).expect("Invalid URL");
        self.media_player.set_media(&media);
        self.media_player.play().unwrap();
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

    pub fn toggle(&self) {
        if self.media_player.is_playing() {
            self.media_player.pause();
        } else {
            let _ = self.media_player.play();
        }
    }

    pub fn stop(&self) {
        self.media_player.stop();
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
        self.state.borrow_mut().queue.clear();
    }

    pub fn playback_state(&self) -> vlc::State {
        self.media_player.state()
    }

    // fn send_event(&mut self, event: PlayerEvent) {
    //     let _ = self.event_sender.send(event);
    // }

    // pub fn subscribe(&self) -> broadcast::Receiver<PlayerEvent> {
    //     self.event_sender.subscribe()
    // }
}
