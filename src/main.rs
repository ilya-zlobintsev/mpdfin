use anyhow::Context;
use async_executor::LocalExecutor;
use async_net::TcpListener;
use futures_lite::{future, StreamExt};
use jellyfin::{user::AuthenticateUserByName, JellyfinClient};
use log::{error, info};
use mpdfin::{
    config::Config,
    database::Database,
    jellyfin::{self, ClientSettings},
    mpd::{Server, SubsystemNotifier},
    player::Player,
};
use std::{cell::RefCell, rc::Rc, str::FromStr};

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

        let player = Player::new(subsystem_notifier.clone(), db.clone());

        let server = Server {
            db,
            jellyfin_client,
            player: Rc::new(player),
            subsystem_notifier,
        };

        let _media_controls = start_media_control(&ex, server.clone());

        let tcp_listener = TcpListener::bind(&config.general.listen_host)
            .await
            .context("Could not start TCP listener")?;
        info!("Listening on {}", config.general.listen_host);

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

        Ok(())
    }))
}

fn start_media_control(ex: &LocalExecutor, server: Server) -> Option<souvlaki::MediaControls> {
    let config = souvlaki::PlatformConfig {
        display_name: env!("CARGO_PKG_NAME"),
        dbus_name: env!("CARGO_PKG_NAME"),
        hwnd: None,
    };

    let (tx, rx) = async_channel::bounded(10);

    ex.spawn(async move {
        while let Ok(event) = rx.recv().await {
            match event {
                souvlaki::MediaControlEvent::Play => server.player.play(),
                souvlaki::MediaControlEvent::Pause => server.player.pause(),
                souvlaki::MediaControlEvent::Toggle => server.player.toggle(),
                souvlaki::MediaControlEvent::Next => todo!(),
                souvlaki::MediaControlEvent::Previous => todo!(),
                souvlaki::MediaControlEvent::Stop => server.player.stop(),
                souvlaki::MediaControlEvent::Seek(_) => todo!(),
                souvlaki::MediaControlEvent::SeekBy(_, _) => todo!(),
                souvlaki::MediaControlEvent::SetPosition(_) => todo!(),
                souvlaki::MediaControlEvent::SetVolume(_) => todo!(),
                souvlaki::MediaControlEvent::OpenUri(_) => todo!(),
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

            controls
                .set_metadata(souvlaki::MediaMetadata {
                    title: Some("Placeholder"),
                    ..Default::default()
                })
                .expect("Could not set media metadata");

            controls
                .set_playback(souvlaki::MediaPlayback::Playing { progress: None })
                .expect("Could not set media playback status");
            Some(controls)
        }
        Err(err) => {
            error!("Could not initialize media keys controls: {err:#}");
            None
        }
    }
}
