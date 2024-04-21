use super::CommandContext;
use crate::mpd::{error::Error, Response, Result};

pub fn playid(ctx: CommandContext<'_>) -> Result<Response> {
    let id = ctx
        .args
        .first()
        .ok_or_else(|| Error::InvalidArg("Missing id".to_owned()))?;

    let url = ctx.server.jellyfin_client.get_audio_stream_url(id);
    ctx.server.player.play_url(&url);

    Ok(Response::new())
}

pub fn pause(ctx: CommandContext<'_>) -> Result<Response> {
    let server = ctx.server;
    match ctx.args.first().map(|s| s.as_str()) {
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

pub fn getvol(ctx: CommandContext<'_>) -> Response {
    Response::new().field("volume", ctx.server.player.volume())
}

pub fn setvol(ctx: CommandContext<'_>) -> Result<Response> {
    let volume = ctx
        .args
        .first()
        .ok_or_else(|| Error::InvalidArg("Missing volume argument".to_owned()))?
        .parse()
        .map_err(|_| Error::InvalidArg("Invalid volume argument".to_owned()))?;
    ctx.server.player.set_volume(volume);
    Ok(Response::new())
}
