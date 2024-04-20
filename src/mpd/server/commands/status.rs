use crate::mpd::{
    server::{read_request, Server},
    Error, Response, Result,
};
use futures_lite::{io::BufReader, AsyncRead, AsyncWrite};

pub fn status() -> Response {
    Response::new()
        .field("volume", 50)
        .field("repeat", 0)
        .field("random", 0)
        .field("single", 0)
        .field("consume", 0)
        .field("playlist", 0)
        .field("playlistlength", 0)
}

pub async fn idle<R: AsyncRead + AsyncWrite + Unpin>(
    _server: &Server,
    stream: &mut BufReader<R>,
) -> Result<Response> {
    let mut buf = String::with_capacity("noidle\n".len());
    match read_request(stream, &mut buf).await.transpose()? {
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
    }
}
