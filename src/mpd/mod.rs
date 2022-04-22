mod commands;
pub mod error;
mod filters;
pub mod model;
pub mod serializer;

use anyhow::anyhow;
use std::collections::BTreeSet;
use std::time::Instant;
use strum::IntoEnumIterator;
use tokio::io::{AsyncBufReadExt, AsyncWriteExt};
use tokio::net::TcpListener;
use tokio::sync::broadcast;
use tokio::{io::BufReader, net::TcpStream};
use tracing::{debug_span, trace_span, Instrument};

use crate::config::Config;
use crate::database::Database;
use crate::playback_server::PlaybackServer;
use model::*;

const MPD_PROTOCOL_VERSION: &str = "0.20.0";

#[derive(Clone)]
pub struct MpdServer {
    db: Database,
    playback_server: PlaybackServer,
    listen_addr: String,
    update_tx: broadcast::Sender<Subsystem>,
    startup_instant: Instant,
}

impl MpdServer {
    pub async fn init(config: Config) -> anyhow::Result<Self> {
        let (update_tx, mut update_rx) = broadcast::channel(16);
        let listen_addr = format!("{}:{}", config.mpd.address, config.mpd.port);

        tokio::spawn(async move {
            while let Ok(subsystem) = update_rx.recv().await {
                tracing::debug!("Subsystem changed: {}", subsystem);
            }
        });

        let db = Database::with_config(&config, update_tx.clone()).await?;

        Ok(Self {
            db: db.clone(),
            playback_server: PlaybackServer::new(config.player.mpris, db, update_tx.clone()),
            update_tx,
            listen_addr,
            startup_instant: Instant::now(),
        })
    }

    pub async fn run(self) -> anyhow::Result<()> {
        let listener = TcpListener::bind(&self.listen_addr)
            .await
            .map_err(|e| anyhow!("failed to bind on {}, {}", self.listen_addr, e))?;

        tracing::info!("Listening on {}", self.listen_addr);

        loop {
            let (stream, _) = listener
                .accept()
                .await
                .expect("Failed to accept TCP stream");

            let server = self.clone();

            tokio::spawn(async move {
                if let Err(e) = server.process_stream(stream).await {
                    tracing::error!("Error processing stream: {}", e);
                }
            });
        }
    }

    async fn process_stream(&self, stream: TcpStream) -> anyhow::Result<()> {
        let span = debug_span!(
            "Client connection",
            address = stream.peer_addr().unwrap().to_string().as_str()
        );

        async {
            let mut stream = BufReader::new(stream);

            stream
                .write(format!("OK MPD {}\n", MPD_PROTOCOL_VERSION).as_bytes())
                .await?;

            let mut tag_types = BTreeSet::new();
            for tag_type in TagType::iter() {
                tag_types.insert(tag_type);
            }

            let mut buf = String::new();

            while stream.read_line(&mut buf).await? != 0 {
                let cmd = buf.trim().to_string();

                match cmd.as_str() {
                    "command_list_begin" => {
                        self.handle_command_list(&mut stream, false, &mut tag_types)
                            .instrument(debug_span!("Processing command list"))
                            .await?
                    }
                    "command_list_ok_begin" => {
                        self.handle_command_list(&mut stream, true, &mut tag_types)
                            .instrument(debug_span!("Processing command ok list"))
                            .await?
                    }
                    _ => {
                        self.handle_command(&mut stream, &cmd, &mut tag_types)
                            .instrument(debug_span!("Processing command", cmd = cmd.as_str()))
                            .await?
                    }
                }

                buf.clear();
            }
            Ok::<(), anyhow::Error>(())
        }
        .instrument(span)
        .await?;

        Ok(())
    }

    async fn handle_command(
        &self,
        stream: &mut BufReader<TcpStream>,
        cmd: &str,
        tag_types: &mut BTreeSet<TagType>,
    ) -> anyhow::Result<()> {
        if cmd == "close" {
            stream.shutdown().await?;
        }

        match self.process_mpd_command(cmd, stream, tag_types).await {
            Ok(response) => {
                if response.is_empty() {
                    tracing::trace!("Empty command response");
                } else {
                    tracing::trace!("MPD command response:\n{}", response.trim());
                    stream.write(response.as_bytes()).await?;
                }
                stream.write(b"OK\n").await?;
            }
            Err(e) => {
                let error = e.to_string(0);
                tracing::trace!("MPD command error: {}", error);
                stream.write(error.as_bytes()).await?;
            }
        }

        Ok(())
    }

    async fn handle_command_list(
        &self,
        stream: &mut BufReader<TcpStream>,
        list_ok: bool,
        tag_types: &mut BTreeSet<TagType>,
    ) -> anyhow::Result<()> {
        let mut commands = self.read_command_list(stream).await?.into_iter();

        for (i, cmd) in commands.by_ref().enumerate() {
            match self
                .process_mpd_command(cmd.trim(), stream, tag_types)
                .instrument(trace_span!(
                    "Processing command list command",
                    i = i,
                    cmd = cmd.as_str()
                ))
                .await
            {
                Ok(response) => {
                    stream.write(response.as_bytes()).await?;

                    if list_ok {
                        stream.write(b"list_OK\n").await?;
                    }

                    tracing::debug!("Finished processing command list command {}", cmd);
                }
                Err(e) => {
                    let error = e.to_string(i);
                    tracing::trace!("MPD command error: {}", error);
                    stream.write(error.as_bytes()).await?;
                    break;
                }
            };
        }

        // If all commands executed succesfully
        if commands.next().is_none() {
            stream.write(b"OK\n").await?;
        } else {
            tracing::debug!("Command list finished with an error");
        }

        Ok(())
    }

    async fn read_command_list(
        &self,
        stream: &mut BufReader<TcpStream>,
    ) -> anyhow::Result<Vec<String>> {
        tracing::debug!("Recording a command list...");

        let mut commands = Vec::new();

        let mut buf = String::new();

        while stream.read_line(&mut buf).await? != 0 {
            let cmd = buf.trim().to_string();

            if cmd == "command_list_end" {
                tracing::debug!("Finished reading command list");
                break;
            }

            tracing::debug!("Adding \"{}\" to command list", cmd);

            commands.push(cmd);

            buf.clear();
        }

        Ok(commands)
    }
}
