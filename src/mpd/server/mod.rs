mod commands;

use super::{Error, Request, Response, Result, MPD_VERSION};
use crate::{database::Database, jellyfin::JellyfinClient, player::Player};
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
}

impl Server {
    pub async fn handle_stream(&self, stream: TcpStream) -> anyhow::Result<()> {
        let peer_addr = stream.peer_addr().context("Unknown peer address")?;
        info!("Got new connection from '{peer_addr}'");

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
                    let result = self.handle_request(request, &mut stream).await;
                    debug!("Response: {result:?}");
                    match result {
                        Ok(response) => {
                            response.write(&mut stream).await?;
                        }
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

    async fn handle_request<R: AsyncRead + AsyncWrite + Unpin>(
        &self,
        request: Request<'_>,
        stream: &mut BufReader<R>,
    ) -> Result<Response> {
        use commands::*;
        let response = match request.command {
            "ping" => Response::new(),
            "status" => status::status(),
            "idle" => status::idle(self, stream).await?,
            "find" => database::find(self, request.args)?,
            "lsinfo" => database::lsinfo(self, request.args),
            "list" => database::list(self, request.args)?,
            "plchanges" => playlist::plchanges(),
            "playid" => playback::playid(self, request.args)?,
            "pause" => playback::pause(self, request.args)?,
            "outputs" => Response::new(),
            "decoders" => Response::new(),
            // Ignore if not currently idling
            "noidle" => Response::new(),
            other => return Err(Error::UnknownCommand(other.to_owned())),
        };
        Ok(response)
    }
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
