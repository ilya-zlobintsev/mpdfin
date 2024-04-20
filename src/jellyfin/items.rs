use serde::Serialize;

use super::base::BaseItemKind;

#[derive(Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct ItemsQuery<'a> {
    pub parent_id: Option<&'a str>,
    pub start_index: Option<i32>,
    pub limit: Option<i32>,
    // This should be an array, but there are issues with query encoding
    pub include_item_types: Option<BaseItemKind>,
    // pub include_item_types: Vec<BaseItemKind>,
    pub recursive: bool,
}
