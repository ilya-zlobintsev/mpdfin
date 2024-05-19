use super::CommandContext;
use crate::{
    database::TreeNode,
    mpd::{
        filters::{FilterExpression, FilterMode},
        Error, Response, Result, Tag,
    },
};
use log::debug;
use std::borrow::Cow;

pub fn find(ctx: CommandContext<'_>) -> Result<Response> {
    let filter = FilterExpression::parse(ctx.args, FilterMode::Find)?;
    let db = ctx.server.db.borrow();
    Ok(db
        .items
        .values()
        .filter(|item| filter.match_item(item, true))
        .fold(Response::new(), |response, item| response.item(item)))
}

pub fn search(ctx: CommandContext<'_>) -> Result<Response> {
    let filter = FilterExpression::parse(ctx.args, FilterMode::Search)?;
    debug!("Searching by expression {filter:?}");
    let db = ctx.server.db.borrow();
    Ok(db
        .items
        .values()
        .filter(|item| filter.match_item(item, false))
        .fold(Response::new(), |response, item| response.item(item)))
}

pub fn lsinfo(ctx: CommandContext<'_>) -> Response {
    let db = ctx.server.db.borrow();

    let request_url = ctx.args.first();
    let url = request_url.as_ref().map(|s| s.as_str()).unwrap_or_default();

    let Some(node) = db.tree_root.navigate(url) else {
        return Response::new();
    };

    let mut response = Response::new();
    match node {
        TreeNode::Directory(dir) => {
            for (name, node) in dir {
                match node {
                    TreeNode::Directory(_) => {
                        let name =
                            if let Some(request_url) = request_url.filter(|url| !url.is_empty()) {
                                Cow::Owned(format!("{request_url}/{name}"))
                            } else {
                                Cow::Borrowed(name)
                            };
                        response.add_field("directory", name);
                    }
                    TreeNode::File(id) => {
                        let item = db.items.get(id).unwrap();
                        response.add_item(item);
                    }
                };
            }
            response
        }
        TreeNode::File(id) => {
            // let name = name.expect("File node always has a name");
            // if let Some(request_url) = request_url {
            //     response.add_field("file", format!("{request_url}/{name}"));
            // } else {
            //     response.add_field("file", name);
            // }

            let item = db.items.get(id).unwrap();
            response.item(item)
        }
    }
}

pub fn list(ctx: CommandContext<'_>) -> Result<Response> {
    let mut args = ctx.args.into_iter();
    let raw_tag = args
        .next()
        .ok_or_else(|| Error::InvalidArg("Missing tag argument".to_owned()))?;
    let tag = Tag::try_from_str(&raw_tag)?;

    let db = ctx.server.db.borrow();

    let mut values = db
        .items
        .values()
        .filter_map(|item| item.get_tag_values(tag))
        .flatten()
        .collect::<Vec<_>>();

    values.sort_unstable();
    values.dedup();

    Ok(Response::new().repeated_field(tag, &values))
}
