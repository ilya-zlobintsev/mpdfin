mod commands;

use super::{subsystem::SubsystemNotifier, Error, Request, Response, Result, MPD_VERSION};
use crate::{
    database::Database, jellyfin::JellyfinClient, mpd::server::commands::CommandContext,
    player::Player,
};
use anyhow::Context;
use async_net::TcpStream;
use futures_lite::{io::BufReader, AsyncBufReadExt, AsyncRead, AsyncWrite, AsyncWriteExt};
use log::{debug, info, trace};
use std::{cell::RefCell, rc::Rc};

#[derive(Clone)]
pub struct Server {
    pub db: Rc<RefCell<Database>>,
    pub jellyfin_client: JellyfinClient,
    pub player: Rc<Player>,
    pub subsystem_notifier: SubsystemNotifier,
}

impl Server {
    pub async fn handle_stream(&self, stream: TcpStream) -> anyhow::Result<()> {
        let peer_addr = stream.peer_addr().context("Unknown peer address")?;
        info!("Got new connection from '{peer_addr}'");

        let mut subsystem_listener = self.subsystem_notifier.listener();

        let mut stream = BufReader::new(stream);

        stream
            .write_all(format!("OK MPD {MPD_VERSION}\n").as_bytes())
            .await
            .context("Could not write greeting")?;

        let mut buf = String::new();

        while let Some(result) = read_request(&mut stream, &mut buf).await {
            match result {
                Ok(request) => {
                    debug!("Handling request {request:?}");
                    let command = request.command;

                    let ctx = CommandContext {
                        server: self,
                        stream: &mut stream,
                        args: request.args,
                        subsystem_listener: &mut subsystem_listener,
                    };
                    let result = handle_request(command, ctx).await;

                    debug!("Response: {result:?}");
                    match result {
                        Ok(Some(response)) => {
                            response.write(&mut stream).await?;
                        }
                        // Special case for `noidle` while not idling, ignore the command and not output anything
                        Ok(None) => (),
                        Err(err) => {
                            stream
                                .write_all(err.to_response(0, Some(command)).as_bytes())
                                .await?
                        }
                    }
                    buf.clear();
                }
                Err(err) => {
                    debug!("Got invalid command: {err}");
                    stream
                        .write_all(err.to_response(0, None).as_bytes())
                        .await?
                }
            }
        }

        info!("Connection from '{peer_addr}' closed");
        Ok(())
    }
}

async fn handle_request(command: &str, ctx: CommandContext<'_>) -> Result<Option<Response>> {
    match command {
        "command_list_begin" => handle_command_list(ctx, false).await,
        "command_list_ok_begin" => handle_command_list(ctx, true).await,
        _ => handle_command(command, ctx).await,
    }
}

async fn handle_command(command: &str, ctx: CommandContext<'_>) -> Result<Option<Response>> {
    use commands::*;
    let response = match command {
        "ping" => Response::new(),

        "status" => status::status(ctx),
        "currentsong" => status::current_song(ctx)?,
        "idle" => status::idle(ctx).await?,
        // Ignore if not currently idling
        "noidle" => return Ok(None),

        "find" => database::find(ctx)?,
        "search" => database::search(ctx)?,
        "lsinfo" => database::lsinfo(ctx),
        "list" => database::list(ctx)?,

        "add" => queue::add(ctx)?,
        "addid" => queue::add_id(ctx)?,
        "playlistinfo" => queue::playlist_info(ctx)?,
        "plchanges" => queue::plchanges(ctx)?,
        "clear" => queue::clear(ctx),

        "play" => playback::play(ctx)?,
        "playid" => playback::playid(ctx)?,
        "pause" => playback::pause(ctx)?,
        "getvol" => playback::get_vol(ctx),
        "setvol" => playback::set_vol(ctx)?,
        "volume" => playback::volume(ctx)?,
        "previous" => playback::previous(ctx)?,
        "next" => playback::next(ctx)?,
        "seek" => playback::seek(ctx)?,
        "seekid" => playback::seekid(ctx)?,
        "seekcur" => playback::seekcur(ctx)?,

        "listplaylists" => playlists::list_playlists(ctx)?,

        "tagtypes" => connection::tag_types(ctx)?,

        "outputs" => Response::new(),
        "decoders" => Response::new(),
        other => return Err(Error::UnknownCommand(other.to_owned())),
    };
    Ok(Some(response))
}

/// Note: it is the caller's responsibility to clear the buffer
async fn read_request<'a, R: AsyncRead + AsyncWrite + Unpin>(
    stream: &mut BufReader<R>,
    buf: &'a mut String,
) -> Option<Result<Request<'a>>> {
    match stream.read_line(buf).await {
        Ok(_) if buf.len() > 1 => {
            let buf = &buf[0..buf.len() - 1];
            trace!("Read buf '{buf}'");
            Some(Request::parse(buf))
        }
        Err(err) => {
            info!("Closing client stream due to error: {err}");
            None
        }
        _ => None,
    }
}

async fn handle_command_list(ctx: CommandContext<'_>, print_ok: bool) -> Result<Option<Response>> {
    let mut buf = String::new();
    let mut requests = Vec::new();

    while let Some(request) = read_request(ctx.stream, &mut buf).await.transpose()? {
        if request.command == "command_list_end" {
            break;
        }

        requests.push((request.command.to_owned(), request.args));
        buf.clear();
    }

    debug!("Processing command list of {} requests", requests.len());

    let mut response = Response::new();

    for (command, args) in requests {
        let ctx = CommandContext {
            args,
            server: ctx.server,
            stream: ctx.stream,
            subsystem_listener: ctx.subsystem_listener,
        };
        if let Some(command_response) = handle_command(&command, ctx).await? {
            response.extend(&command_response);
        }

        if print_ok {
            response.add_list_ok();
        }
    }

    Ok(Some(response))
}
