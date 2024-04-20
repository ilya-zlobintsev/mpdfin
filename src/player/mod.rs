pub mod event;

use vlc::{Instance, Media, MediaPlayer};

pub struct Player {
    instance: Instance,
    media_player: MediaPlayer,
    // event_sender: broadcast::Sender<PlayerEvent>,
}

impl Player {
    pub fn new() -> Self {
        let instance = Instance::new().expect("Could not initialize instance");

        let user_agent = env!("CARGO_PKG_NAME");
        instance.set_user_agent(user_agent, user_agent);

        let media_player = MediaPlayer::new(&instance).expect("Could not initialize media player");
        // let (event_sender, _) = broadcast::channel(10);

        Self {
            instance,
            media_player,
            // event_sender,
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

    // fn send_event(&mut self, event: PlayerEvent) {
    //     let _ = self.event_sender.send(event);
    // }

    // pub fn subscribe(&self) -> broadcast::Receiver<PlayerEvent> {
    //     self.event_sender.subscribe()
    // }
}

impl Default for Player {
    fn default() -> Self {
        Self::new()
    }
}
