use super::CommandContext;
use crate::mpd::{Response, Result};

pub fn list_playlists(_ctx: CommandContext<'_>) -> Result<Response> {
    Ok(Response::new())
}
