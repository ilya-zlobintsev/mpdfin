use anyhow::Context;
use async_executor::LocalExecutor;
use async_net::TcpListener;
use async_signal::{Signal, Signals};
use futures_lite::{future, StreamExt};
use gstreamer_play::PlayState;
use jellyfin::{user::AuthenticateUserByName, JellyfinClient};
use log::{debug, error, info};
use mpdfin::{
    config::Config,
    database::Database,
    jellyfin::{self, ClientSettings},
    mpd::{Server, Subsystem, SubsystemNotifier},
    player::{self, Player},
};
use souvlaki::MediaPosition;
use std::{cell::RefCell, rc::Rc, str::FromStr, time::Duration};

fn main() -> anyhow::Result<()> {
    let ex = LocalExecutor::new();
    future::block_on(ex.run(async {
        let config = Config::load()?;

        let max_log_level = log::Level::from_str(&config.general.log_level)
            .with_context(|| format!("Invalid log level '{}'", config.general.log_level))?;
        simple_logger::init_with_level(max_log_level).context("Could not init logger")?;

        let (db, jellyfin_client) =
            match Database::load().context("Could not load existing database")? {
                Some(existing_db) => {
                    info!("Loaded database from file");
                    let client_settings = ClientSettings::new(Some(existing_db.token.to_string()));
                    let jellyfin_client =
                        JellyfinClient::new(config.jellyfin.url.clone(), client_settings);
                    (existing_db, jellyfin_client)
                }
                None => {
                    let settings = ClientSettings::new(None);

                    let auth_jellyfin_client =
                        JellyfinClient::new(config.jellyfin.url.clone(), settings);
                    let auth_result = auth_jellyfin_client
                        .authenticate_by_name(AuthenticateUserByName {
                            username: config.jellyfin.username,
                            pw: config.jellyfin.password,
                        })
                        .context("Could not authenticate")?;

                    let current_user = auth_result.user;
                    info!(
                        "Authenticated as {}",
                        current_user
                            .name
                            .as_ref()
                            .expect("Jellyfin user has no name")
                    );

                    let settings = ClientSettings::new(Some(auth_result.access_token));
                    let jellyfin_client = JellyfinClient::new(config.jellyfin.url, settings);

                    info!("Creating new database");
                    let db = Database::new(&jellyfin_client, &current_user)?;
                    db.save()?;
                    (db, jellyfin_client)
                }
            };
        let db = Rc::new(RefCell::new(db));

        let subsystem_notifier = SubsystemNotifier::new();

        let state = player::State::load().unwrap_or_default();

        let player = Rc::new(Player::new(
            subsystem_notifier.clone(),
            db.clone(),
            jellyfin_client.clone(),
            state,
            // ex,
        ));

        let server = Server {
            db,
            jellyfin_client,
            player: player.clone(),
            subsystem_notifier: subsystem_notifier.clone(),
        };

        start_media_control(&ex, server.clone());

        let tcp_listener = TcpListener::bind(&config.general.listen_host)
            .await
            .context("Could not start TCP listener")?;
        info!("Listening on {}", config.general.listen_host);

        let task_player = player.clone();
        ex.spawn(async move {
            let mut listener = subsystem_notifier.listener();
            loop {
                listener
                    .listen(&[
                        Subsystem::Playlist,
                        Subsystem::Player,
                        Subsystem::Mixer,
                        Subsystem::Options,
                    ])
                    .await;
                task_player.save_state();
            }
        })
        .detach();

        let _main_task = ex.spawn(async move {
            let ex = LocalExecutor::new();
            ex.run(async {
                while let Some(stream) = tcp_listener.incoming().next().await {
                    match stream {
                        Ok(stream) => {
                            let server = server.clone();
                            ex.spawn(async move {
                                if let Err(err) = server.handle_stream(stream).await {
                                    error!("Error processing stream: {err:#}");
                                }
                            })
                            .detach();
                        }
                        Err(err) => error!("Could not accept TCP connection: {err}"),
                    }
                }
            })
            .await
        });

        let mut exit_signals = Signals::new([Signal::Int, Signal::Term, Signal::Quit])?;
        exit_signals.next().await;

        info!("Got signal, exiting");
        player.save_state();

        Ok(())
    }))
}

fn start_media_control(ex: &LocalExecutor, server: Server) {
    let config = souvlaki::PlatformConfig {
        display_name: env!("CARGO_PKG_NAME"),
        dbus_name: env!("CARGO_PKG_NAME"),
        hwnd: None,
    };

    let (tx, rx) = async_channel::bounded(10);

    let player = server.player.clone();
    ex.spawn(async move {
        while let Ok(event) = rx.recv().await {
            match event {
                souvlaki::MediaControlEvent::Play => player.play(),
                souvlaki::MediaControlEvent::Pause => player.pause(),
                souvlaki::MediaControlEvent::Toggle => player.toggle(),
                souvlaki::MediaControlEvent::Next => player.next(),
                souvlaki::MediaControlEvent::Previous => player.previous(),
                souvlaki::MediaControlEvent::Stop => player.stop(),
                souvlaki::MediaControlEvent::SetVolume(value) => {
                    player.set_volume((value * 100.0) as i32);
                }
                souvlaki::MediaControlEvent::OpenUri(_) => todo!(),
                souvlaki::MediaControlEvent::SetPosition(position) => {
                    player.seek(position.0.as_millis() as f64 / 1000.0);
                }
                _ => (),
            }
        }
    })
    .detach();

    match souvlaki::MediaControls::new(config) {
        Ok(mut controls) => {
            controls
                .attach(move |event| {
                    let _ = tx.try_send(event);
                })
                .expect("Could not attach media key events");

            let mut subsystem_listener = server.subsystem_notifier.listener();
            ex.spawn(async move {
                loop {
                    subsystem_listener.listen(&[Subsystem::Player]).await;

                    let mut metadata = souvlaki::MediaMetadata::default();

                    #[allow(unused_assignments)] // Stores the value that metadata references
                    let mut artist = None;

                    let db = server.db.borrow();
                    if let Some(item_id) = server.player.state().current_item_id() {
                        if let Some(item) = db.items.get(item_id) {
                            metadata.title = item.name.as_deref();
                            metadata.album = item.album.as_deref();
                            // metadata.duration = item
                            //     .run_time_ticks
                            //     .map(|ticks| Duration::from_millis(ticks / 10000));

                            artist = item.album_artist.clone().or_else(|| {
                                if item.artists.is_empty() {
                                    None
                                } else {
                                    Some(item.artists.join(", "))
                                }
                            });
                            metadata.artist = artist.as_deref();
                        }
                    }

                    let progress = server
                        .player
                        .media_position()
                        .map(|position| MediaPosition(Duration::from_millis(position)));
                    let playback = match server.player.playback_state() {
                        PlayState::Playing => souvlaki::MediaPlayback::Playing { progress },
                        PlayState::Paused => souvlaki::MediaPlayback::Paused { progress },
                        _ => souvlaki::MediaPlayback::Stopped,
                    };
                    debug!("Updating system media plabyack status to {playback:?}");

                    controls
                        .set_playback(playback)
                        .expect("Could not set media playback status");

                    controls
                        .set_metadata(metadata)
                        .expect("Could not set media metadata");
                }
            })
            .detach();
        }
        Err(err) => {
            error!("Could not initialize media keys controls: {err:#}");
        }
    }
}
