use super::CommandContext;
use crate::mpd::{error::Error, Response, Result};
use std::rc::Rc;

pub fn plchanges(ctx: CommandContext<'_>) -> Result<Response> {
    let version = ctx
        .args
        .first()
        .ok_or_else(|| Error::InvalidArg("Missing version argument".to_owned()))?
        .parse::<u64>()
        .map_err(|_| Error::InvalidArg("Invalid version provided".to_owned()))?;

    if version < ctx.player().state().playlist_version {
        playlist_info(ctx)
    } else {
        Ok(Response::new())
    }
}

pub fn clear(ctx: CommandContext<'_>) -> Response {
    ctx.player().clear();
    Response::new()
}

pub fn add(ctx: CommandContext<'_>) -> Result<Response> {
    add_id(ctx)?;
    Ok(Response::new())
}

pub fn add_id(ctx: CommandContext<'_>) -> Result<Response> {
    let mut args = ctx.args.into_iter();

    let item_id: Rc<str> = args
        .next()
        .ok_or_else(|| Error::InvalidArg("Missing song uri".to_owned()))?
        .into();

    let id = ctx
        .server
        .player
        .add_item(item_id.clone())
        .ok_or_else(|| Error::InvalidArg(format!("Item '{item_id}' not found")))?;

    Ok(Response::new().field("Id", id))
}

pub fn playlist_info(ctx: CommandContext<'_>) -> Result<Response> {
    let db = ctx.server.db.borrow();
    let queue = &ctx.server.player.state().queue;

    Ok(queue.iter().enumerate().fold(
        Response::new(),
        |response, (pos, (queue_id, queue_item))| {
            println!("Item id: '{}'", queue_item.item_id);
            println!("db size: {}", db.items.len());
            let item = db
                .items
                .get(&queue_item.item_id)
                .expect("ID must be present in database");
            response.item(item).field("Pos", pos).field("Id", queue_id)
        },
    ))
}
