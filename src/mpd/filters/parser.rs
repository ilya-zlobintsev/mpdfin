use super::FilterExpression;
use crate::mpd::{Error, Result, Tag};
use std::str::CharIndices;

impl FilterExpression {
    pub fn parse(args: impl IntoIterator<Item = String>) -> Result<Self> {
        let mut args = args.into_iter();

        let first_arg = args
            .next()
            .ok_or_else(|| Error::InvalidArg("Missing filter argument".to_owned()))?;

        // If input does not start with '(', parse it like the old simple filter syntax
        if first_arg.starts_with('(') {
            Self::parse_inner(&first_arg)
        } else {
            let value = args
                .next()
                .ok_or_else(|| Error::InvalidArg("Missing filter value".to_owned()))?;

            let tag = Tag::try_from_str(&first_arg)?;

            let mut filters = vec![Self::TagMatch(tag, value)];

            while let Some(raw_tag) = args.next() {
                let value = args
                    .next()
                    .ok_or_else(|| Error::InvalidArg("Missing filter value".to_owned()))?;
                let tag = Tag::try_from_str(&raw_tag)?;
                filters.push(Self::TagMatch(tag, value));
            }

            Ok(Self::And(filters))
        }
    }

    fn parse_inner(input: &str) -> Result<Self> {
        if input.as_bytes().first() != Some(&b'(') {
            return Err(Error::InvalidArg(format!(
                "Filter must start with '(', got '{input}'"
            )));
        }

        if input.len() == 1 {
            return Err(Error::InvalidArg("Unexpected EOL in filter".to_owned()));
        }
        let input = &input[1..input.len() - 1];

        if input.as_bytes()[0] == b'!' {
            if input.len() == 1 {
                return Err(Error::InvalidArg(
                    "Missing expression after negation".to_owned(),
                ));
            }
            let inner = Self::parse_inner(&input[1..])?;
            return Ok(Self::Not(Box::new(inner)));
        }

        let (term_1, input) = next_term(input)?
            .ok_or_else(|| Error::InvalidArg("Unexpected EOL in filter".to_owned()))?;
        let (term_2, input) = next_term(input)?
            .ok_or_else(|| Error::InvalidArg("Unexpected EOL in filter".to_owned()))?;

        match term_2.as_str() {
            "==" => equality_expression(&term_1, false, input),
            "!=" => equality_expression(&term_1, false, input),
            "AND" => {
                let lhs = Self::parse_inner(&term_1)?;
                let (rhs_raw, _) = next_term(input)?.ok_or_else(|| {
                    Error::InvalidArg("Missing right-hand-side expression in AND".to_owned())
                })?;
                let rhs = Self::parse_inner(&rhs_raw)?;
                Ok(Self::And(vec![lhs, rhs]))
            }
            _ => match term_1.as_str() {
                "base" => Ok(Self::BaseDir(term_2)),
                _ => Err(Error::InvalidArg(
                    "Could not recognize filter expression type".to_owned(),
                )),
            },
        }
    }
}

fn equality_expression(term_1: &str, reverse: bool, input: &str) -> Result<FilterExpression> {
    let (value, _) = next_term(input)?
        .ok_or_else(|| Error::InvalidArg("Missing value for equality in filter".to_owned()))?;
    match term_1 {
        "file" => {
            if reverse {
                return Err(Error::InvalidArg("Cannot mismatch by item url".to_owned()));
            }
            Ok(FilterExpression::UriMatch(value))
        }
        "AudioFormat" => Err(Error::InvalidArg("Unsupported filter type".to_owned())),
        _ => {
            let tag = Tag::try_from_str(term_1)?;
            if reverse {
                Ok(FilterExpression::TagMismatch(tag, value))
            } else {
                Ok(FilterExpression::TagMatch(tag, value))
            }
        }
    }
}

fn next_term(input: &str) -> Result<Option<(String, &str)>> {
    let mut term = String::new();

    let mut chars = input.char_indices();

    while let Some((i, current)) = chars.next() {
        match current {
            '\\' => {
                let (_, next_c) = chars.next().ok_or_else(|| {
                    Error::InvalidArg("Missing symbol after escape character in filter".to_owned())
                })?;
                term.push(next_c);
            }
            ' ' | '\t' => {
                if i != 0 {
                    return Ok(Some((term, &input[i + 1..])));
                }
            }
            '\'' | '"' => {
                let end_char = current;
                let i = consume_until_match(&mut chars, &mut term, end_char)?;
                return Ok(Some((term, &input[i + 1..])));
            }
            '(' => {
                term.push('(');
                let i = consume_until_match(&mut chars, &mut term, ')')?;
                term.push(')');
                return Ok(Some((term, &input[i + 1..])));
            }
            _ => term.push(current),
        }
    }

    if term.is_empty() {
        Ok(None)
    } else {
        Ok(Some((term, "")))
    }
}

fn consume_until_match(
    chars: &mut CharIndices<'_>,
    term: &mut String,
    end_char: char,
) -> Result<usize> {
    while let Some((i, current)) = chars.next() {
        match current {
            '\\' => {
                let (_, next_c) = chars.next().ok_or_else(|| {
                    Error::InvalidArg("Missing symbol after escape character in filter".to_owned())
                })?;
                term.push(next_c);
            }
            _ if current == end_char => {
                return Ok(i);
            }
            _ => term.push(current),
        }
    }
    Err(Error::InvalidArg(
        "Unterminated literal".to_ascii_lowercase(),
    ))
}

#[cfg(test)]
mod tests {
    use super::FilterExpression;
    use crate::mpd::Tag;

    #[test]
    fn uri_match() {
        let filter = FilterExpression::parse(["(file == 'A/B/C')".to_owned()]).unwrap();
        assert_eq!(FilterExpression::UriMatch("A/B/C".to_owned()), filter);
    }

    #[test]
    fn tag_match_escaped_quotes() {
        let filter = FilterExpression::parse([r#"(Artist == "foo\'bar\"")"#.to_owned()]).unwrap();
        assert_eq!(
            FilterExpression::TagMatch(Tag::Artist, r#"foo'bar""#.to_owned()),
            filter
        );
    }

    #[test]
    fn base_dir() {
        let filter = FilterExpression::parse(["(base 'A/B')".to_owned()]).unwrap();
        assert_eq!(FilterExpression::BaseDir("A/B".to_owned()), filter);
    }

    #[test]
    fn not_base_dir() {
        let filter = FilterExpression::parse(["(!(base 'A/B'))".to_owned()]).unwrap();
        assert_eq!(
            FilterExpression::Not(Box::new(FilterExpression::BaseDir("A/B".to_owned()))),
            filter
        );
    }

    #[test]
    fn album_and_artist() {
        let filter =
            FilterExpression::parse(["((artist == 'foo') AND (album == 'bar'))".to_owned()])
                .unwrap();
        assert_eq!(
            FilterExpression::And(vec![
                FilterExpression::TagMatch(Tag::Artist, "foo".to_owned()),
                FilterExpression::TagMatch(Tag::Album, "bar".to_owned())
            ],),
            filter
        );
    }

    #[test]
    fn album_and_artist_old_format() {
        let filter =
            FilterExpression::parse(["artist", "foo", "album", "bar"].map(str::to_owned)).unwrap();
        assert_eq!(
            FilterExpression::And(vec![
                FilterExpression::TagMatch(Tag::Artist, "foo".to_owned()),
                FilterExpression::TagMatch(Tag::Album, "bar".to_owned())
            ],),
            filter
        );
    }
}
