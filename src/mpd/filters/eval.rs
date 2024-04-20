use super::FilterExpression;
use crate::{jellyfin::base::BaseItemDto, mpd::tag::Tag};

impl FilterExpression {
    pub fn match_item(&self, item: &BaseItemDto, case_sensitive: bool) -> bool {
        match self {
            FilterExpression::TagMatch(tag, wanted_value) => {
                match_tag_value(item, *tag, wanted_value, case_sensitive)
            }
            FilterExpression::TagMismatch(tag, not_wanted_value) => {
                !match_tag_value(item, *tag, not_wanted_value, case_sensitive)
            }
            FilterExpression::UriMatch(_) => todo!(),
            FilterExpression::BaseDir(_) => todo!(),
            FilterExpression::And(subexpr) => subexpr
                .iter()
                .all(|subexpr| subexpr.match_item(item, case_sensitive)),
            FilterExpression::Not(subexpr) => !subexpr.match_item(item, case_sensitive),
        }
    }
}

fn match_tag_value(item: &BaseItemDto, tag: Tag, wanted_value: &str, case_sensitive: bool) -> bool {
    let item_values = item.get_tag_values(tag);
    match (item_values.as_deref(), wanted_value) {
        (None, "") | (Some([]), "") => true,
        (Some(values), wanted_value) => {
            if case_sensitive {
                values.iter().any(|item_value| item_value == wanted_value)
            } else {
                let wanted_value = wanted_value.to_lowercase();
                values
                    .iter()
                    .map(|value| value.to_lowercase())
                    .any(|value| value == wanted_value)
            }
        }
        (None, _) => false,
    }
}

#[cfg(test)]
mod tests {
    use crate::{
        jellyfin::base::{BaseItemDto, BaseItemKind},
        mpd::{filters::FilterExpression, tag::Tag},
    };

    #[test]
    fn match_artist_basic() {
        let item = BaseItemDto {
            id: String::new().into(),
            name: None,
            r#type: BaseItemKind::Audio,
            collection_type: None,
            album: None,
            artists: vec!["foo".into()],
            album_artist: None,
            genres: vec![],
            index_number: None,
            premiere_date: None,
        };
        let filter = FilterExpression::TagMatch(Tag::Artist, "foo".to_owned());
        assert!(filter.match_item(&item, true));
    }

    #[test]
    fn mismatch_artist_basic() {
        let item = BaseItemDto {
            id: String::new().into(),
            name: None,
            r#type: BaseItemKind::Audio,
            collection_type: None,
            album: None,
            artists: vec!["foo".into()],
            album_artist: None,
            genres: vec![],
            index_number: None,
            premiere_date: None,
        };
        let filter = FilterExpression::TagMismatch(Tag::Artist, "bar".to_owned());
        assert!(filter.match_item(&item, true));
    }
}
