use crate::mpd::{error::Error, server::Server, Response, Result};

pub fn playid(server: &Server, args: Vec<String>) -> Result<Response> {
    let id = args
        .first()
        .ok_or_else(|| Error::InvalidArg("Missing id".to_owned()))?;

    let url = server.jellyfin_client.get_audio_stream_url(id);
    server.player.play_url(&url);

    Ok(Response::new())
}

pub fn pause(server: &Server, args: Vec<String>) -> Result<Response> {
    match args.first().map(|s| s.as_str()) {
        Some("1") => {
            if server.player.is_playing() {
                server.player.pause();
            }
        }
        Some("0") => {
            if !server.player.is_playing() {
                server.player.play();
            }
        }
        None => {
            server.player.toggle();
        }
        _ => return Err(Error::InvalidArg("Invalid pause state".to_owned())),
    }
    Ok(Response::new())
}
