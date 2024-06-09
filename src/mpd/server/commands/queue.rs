use super::CommandContext;
use crate::mpd::{error::Error, Response, Result};

pub fn plchanges(ctx: CommandContext<'_>) -> Result<Response> {
    let version = ctx
        .args
        .first()
        .ok_or_else(|| Error::InvalidArg("Missing version argument".to_owned()))?
        .parse::<u64>()
        .map_err(|_| Error::InvalidArg("Invalid version provided".to_owned()))?;

    if version < ctx.player().state().playlist_version() {
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

    let url = args
        .next()
        .ok_or_else(|| Error::InvalidArg("Missing song uri".to_owned()))?;

    let db = ctx.server.db.borrow();

    if let Some(id) = ctx.server.player.add_item(url.clone().into()) {
        Ok(Response::new().field("Id", id))
    } else {
        let node = db
            .tree_root
            .navigate(&url)
            .ok_or_else(|| Error::InvalidArg(format!("Item '{url}' not found")))?;

        let mut items = node.item_ids();
        items.sort_unstable_by_key(|item_id| {
            db.items.get(item_id).map(|item| {
                (
                    &item.premiere_date,
                    &item.artists,
                    &item.album_artist,
                    &item.album,
                    &item.index_number,
                    &item.name,
                )
            })
        });

        let mut ids = Vec::with_capacity(items.len());
        for item_id in items {
            if let Some(queue_id) = ctx.server.player.add_item(item_id) {
                ids.push(queue_id);
            }
        }

        Ok(Response::new().repeated_field("Id", &ids))
    }
}

pub fn playlist_info(ctx: CommandContext<'_>) -> Result<Response> {
    let db = ctx.server.db.borrow();
    let state = ctx.server.player.state();
    let queue = &state.queue();

    Ok(queue.iter().enumerate().fold(
        Response::new(),
        |response, (pos, (queue_id, queue_item))| {
            let item = db
                .items
                .get(&queue_item.item_id)
                .expect("ID must be present in database");
            response.item(item).field("Pos", pos).field("Id", queue_id)
        },
    ))
}
