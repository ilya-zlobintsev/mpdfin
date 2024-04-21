use crate::mpd::{subsystem::SubsystemNotifier, Subsystem};
use vlc::{Instance, Media, MediaPlayer, MediaPlayerAudioEx};

pub struct Player {
    instance: Instance,
    media_player: MediaPlayer,
    subsystem_notifier: SubsystemNotifier,
}

impl Player {
    pub fn new(subsystem_notifier: SubsystemNotifier) -> Self {
        let instance = Instance::new().expect("Could not initialize instance");

        let user_agent = env!("CARGO_PKG_NAME");
        instance.set_user_agent(user_agent, user_agent);

        let media_player = MediaPlayer::new(&instance).expect("Could not initialize media player");
        // let (event_sender, _) = broadcast::channel(10);

        // let event_manager = media_player.event_manager();
        // event_manager.attach(EventType::, callback)

        Self {
            instance,
            media_player,
            subsystem_notifier,
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

    // fn send_event(&mut self, event: PlayerEvent) {
    //     let _ = self.event_sender.send(event);
    // }

    // pub fn subscribe(&self) -> broadcast::Receiver<PlayerEvent> {
    //     self.event_sender.subscribe()
    // }
}
