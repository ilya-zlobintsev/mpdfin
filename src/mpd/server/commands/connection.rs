use super::CommandContext;
use crate::mpd::{tag::Tag, Response, Result};
use strum::VariantArray;

pub fn tag_types(_ctx: CommandContext<'_>) -> Result<Response> {
    Ok(Response::new().repeated_field("tagtype", Tag::VARIANTS))
}
