use souvlaki::{MediaControlEvent, MediaControls, MediaMetadata, PlatformConfig};
use tokio::sync::broadcast;

use self::{audio_server::AudioServer, events::MediaEvent, options::PlaybackOptions, queue::Queue};
use crate::{
    database::Database,
    mpd::model::{Decoder, Output, State, Status, Subsystem},
};

mod audio_server;
mod events;
pub mod options;
pub mod queue;

#[derive(Clone)]
pub struct PlaybackServer {
    options: PlaybackOptions,
    pub queue: Queue,
    audio_server: AudioServer,
    update_tx: broadcast::Sender<Subsystem>,
    db: Database,
}

impl PlaybackServer {
    pub fn new(mpris: bool, db: Database, update_tx: broadcast::Sender<Subsystem>) -> Self {
        let server = Self {
            options: Default::default(),
            queue: Queue::new(update_tx.clone()),
            db,
            audio_server: AudioServer::new(),
            update_tx,
        };

        #[cfg(not(target_os = "windows"))]
        let hwnd = None;

        let media_config = PlatformConfig {
            dbus_name: "mpdfin",
            display_name: "mpdfin",
            hwnd,
        };
        let (tx, rx) = std::sync::mpsc::channel();
        let mut media_controls =
            MediaControls::new(media_config).expect("Failed to initialize media controls");

        if mpris {
            media_controls
                .attach(move |event| {
                    tx.send(MediaEvent::MediaControlEvent(event))
                        .expect("Failed to send media control event")
                })
                .unwrap();
        }

        {
            let server = server.clone();
            tokio::task::spawn_blocking(move || {
                while let Ok(event) = rx.recv() {
                    tracing::trace!("Received event: {:?}", event);
                    match event {
                        MediaEvent::MediaControlEvent(control_event) => match control_event {
                            MediaControlEvent::Play => server.play(),
                            MediaControlEvent::Pause => server.pause(),
                            MediaControlEvent::Toggle => server.toggle_playback(),
                            MediaControlEvent::Next => todo!(),
                            MediaControlEvent::Previous => todo!(),
                            MediaControlEvent::Stop => todo!(),
                            _ => (),
                        },
                        MediaEvent::SubsystemUpdate(subsystem) => match subsystem {
                            Subsystem::Options => {
                                media_controls
                                    .set_metadata(MediaMetadata {
                                        title: todo!(),
                                        album: todo!(),
                                        artist: todo!(),
                                        cover_url: todo!(),
                                        duration: todo!(),
                                    })
                                    .expect("Failed to set metadata");
                            }
                            _ => (),
                        },
                    }
                }
            });
        }

        server
    }

    pub fn play(&self) {
        tracing::trace!("Current state: {:?}", self.state());
        if !self.queue.is_empty() && self.state() != State::Play {
            tracing::trace!("Starting playback");
            self.set_state(State::Play);

            if let Some(current_item) = self.queue.get_current_item() {
                let db = self.db.clone();
                let audio_server = self.audio_server.clone();

                tokio::spawn(async move {
                    match db.get_audio_file(&current_item.song.filename).await {
                        Ok(file_path) => {
                            audio_server.play_file(&file_path.display().to_string());
                            audio_server.unpause();
                        }
                        Err(e) => tracing::error!("Failed to get audio file: {}", e),
                    }
                });
            }
            self.update_tx
                .send(Subsystem::Player)
                .expect("Failed to send subsystem update");
        }
    }

    pub fn pause(&self) {
        if self.state() == State::Play {
            self.set_state(State::Pause);
            self.audio_server.pause();
            self.update_tx
                .send(Subsystem::Player)
                .expect("Failed to send subsystem update");
        }
    }

    pub fn toggle_playback(&self) {
        match self.state() {
            State::Play => self.pause(),
            State::Pause => self.play(),
            State::Stop => (),
        }
        self.update_tx
            .send(Subsystem::Player)
            .expect("Failed to send subsystem update");
    }

    // pub fn play<R>(&self, input: R) -> Result<Sink, PlayError>
    // where
    //     R: Read + Seek + Send + 'static,
    // {

    //     // wav.load_mem(input)

    //     // self.stream_handle.play_once(input)
    // }

    /*pub async fn play_response(&self, response: Bytes) {
        let cursor = Cursor::new(stream_response.bytes().await.unwrap());

        let reader = BufReader::new(cursor);

        let plaback = playback_server.play(reader).unwrap();
    }*/

    pub fn status(&self) -> Status {
        let current_item = self.queue.get_current_item();
        let current_item = current_item.as_ref();

        Status {
            repeat: self.repeat(),
            random: self.random(),
            single: self.single(),
            consume: self.consume(),
            volume: 0,
            state: self.state(),
            playlist: 0,
            playlist_length: 0,
            duration: current_item.and_then(|item| Some(item.song.duration)),
            song: current_item.and_then(|item| Some(item.pos)),
            song_id: current_item.and_then(|item| Some(item.id)),
            ..Default::default()
        }
    }

    pub fn get_outputs(&self) -> Vec<Output> {
        vec![Output {
            output_id: 0,
            output_name: "default",
            output_enabled: true,
        }]
    }

    pub fn get_decoders(&self) -> Vec<Decoder> {
        vec![Decoder {
            plugin: "mad",
            suffix: "mp3",
            mime_type: vec!["audio/mpeg"],
        }]
    }
}
