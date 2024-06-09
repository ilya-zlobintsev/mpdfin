use super::CommandContext;
use crate::mpd::{error::Error, Response, Result};

pub fn play(ctx: CommandContext<'_>) -> Result<Response> {
    match ctx.args.first() {
        Some(arg) => {
            let pos: usize = arg
                .parse()
                .map_err(|_| Error::InvalidArg("Invalid position provided".to_owned()))?;
            ctx.player().play_by_pos(pos);
        }
        None => {
            ctx.player().play();
        }
    }

    Ok(Response::new())
}

pub fn playid(ctx: CommandContext<'_>) -> Result<Response> {
    let queue_id = ctx
        .args
        .first()
        .ok_or_else(|| Error::InvalidArg("Missing id".to_owned()))?
        .parse::<u64>()
        .map_err(|_| Error::InvalidArg("Invalid item id".to_owned()))?;

    ctx.server.player.play_by_id(queue_id);

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

pub fn get_vol(ctx: CommandContext<'_>) -> Response {
    Response::new().field("volume", ctx.server.player.volume())
}

pub fn set_vol(ctx: CommandContext<'_>) -> Result<Response> {
    let volume = ctx
        .args
        .first()
        .ok_or_else(|| Error::InvalidArg("Missing volume argument".to_owned()))?
        .parse()
        .map_err(|_| Error::InvalidArg("Invalid volume argument".to_owned()))?;
    ctx.server.player.set_volume(volume);
    Ok(Response::new())
}

pub fn volume(ctx: CommandContext<'_>) -> Result<Response> {
    let change: i32 = ctx
        .args
        .first()
        .ok_or_else(|| Error::InvalidArg("Missing volume argument".to_owned()))?
        .parse()
        .map_err(|_| Error::InvalidArg("Invalid volume argument".to_owned()))?;

    let new_volume = ctx.server.player.volume() + change;
    ctx.server.player.set_volume(new_volume);
    Ok(Response::new())
}

pub fn previous(ctx: CommandContext<'_>) -> Result<Response> {
    ctx.player().previous();
    Ok(Response::new())
}

pub fn next(ctx: CommandContext<'_>) -> Result<Response> {
    ctx.player().next();
    Ok(Response::new())
}

pub fn seek(ctx: CommandContext<'_>) -> Result<Response> {
    let pos = ctx
        .args
        .first()
        .ok_or_else(|| Error::InvalidArg("Position was not provided".to_owned()))?
        .parse::<usize>()
        .map_err(|_| Error::InvalidArg("Invalid position provided".to_owned()))?;

    let time = ctx
        .args
        .get(1)
        .ok_or_else(|| Error::InvalidArg("Time was not provided".to_owned()))?
        .parse::<f64>()
        .map_err(|_| Error::InvalidArg("Invalid time provided".to_owned()))?;

    if ctx.player().state().current_pos() != Some(pos) {
        ctx.player().play_by_pos(pos);
    }

    ctx.player().seek(time);

    Ok(Response::new())
}

pub fn seekid(ctx: CommandContext<'_>) -> Result<Response> {
    let id = ctx
        .args
        .first()
        .ok_or_else(|| Error::InvalidArg("Position was not provided".to_owned()))?
        .parse::<u64>()
        .map_err(|_| Error::InvalidArg("Invalid position provided".to_owned()))?;

    let time = ctx
        .args
        .get(1)
        .ok_or_else(|| Error::InvalidArg("Time was not provided".to_owned()))?
        .parse::<f64>()
        .map_err(|_| Error::InvalidArg("Invalid time provided".to_owned()))?;

    if ctx.player().state().current_id() != Some(id) {
        ctx.player().play_by_id(id);
    }

    ctx.player().seek(time);

    Ok(Response::new())
}

pub fn seekcur(ctx: CommandContext<'_>) -> Result<Response> {
    let time = ctx
        .args
        .first()
        .ok_or_else(|| Error::InvalidArg("Time was not provided".to_owned()))?
        .parse::<f64>()
        .map_err(|_| Error::InvalidArg("Invalid time provided".to_owned()))?;

    ctx.player().seek(time);

    Ok(Response::new())
}
