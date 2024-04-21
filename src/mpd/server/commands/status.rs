use super::CommandContext;
use crate::mpd::{error::Error, server::read_request, Request, Response, Result, Subsystem};
use futures_lite::future;

pub fn status(ctx: CommandContext<'_>) -> Response {
    Response::new()
        .field("volume", ctx.server.player.volume())
        .field("repeat", 0)
        .field("random", 0)
        .field("single", 0)
        .field("consume", 0)
        .field("playlist", 0)
        .field("playlistlength", 0)
}

pub async fn idle(ctx: CommandContext<'_>) -> Result<Response> {
    let mut buf = String::with_capacity("noidle\n".len());

    enum IdleInterruption<'a> {
        Request(Result<Option<Request<'a>>>),
        Notification(Vec<Subsystem>),
    }

    let interruption = future::or(
        async { IdleInterruption::Request(read_request(ctx.stream, &mut buf).await.transpose()) },
        async { IdleInterruption::Notification(ctx.subsystem_listener.listen_all().await) },
    )
    .await;

    match interruption {
        IdleInterruption::Request(request) => match request? {
            Some(request) => {
                if request.command == "noidle" {
                    Ok(Response::new())
                } else {
                    Err(Error::InvalidArg(
                        "Only the 'noidle' command is allowed when idling".to_owned(),
                    ))
                }
            }
            None => Ok(Response::new()),
        },
        IdleInterruption::Notification(subsystems) => {
            Ok(Response::new().repeated_field("changed", &subsystems))
        }
    }
}
