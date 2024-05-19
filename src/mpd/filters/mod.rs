mod eval;
mod parser;

use super::Tag;

#[derive(Debug, PartialEq, Eq)]
pub enum FilterExpression {
    TagMatch(Tag, String),
    TagMismatch(Tag, String),
    TagContains(Tag, String),
    UriMatch(String),
    BaseDir(String),
    // This is a vector instead of simply having two values for compatibility with the old filter syntax
    // as the old (simple) filter format gets parsed into an `AND` expression
    And(Vec<FilterExpression>),
    Not(Box<FilterExpression>),
}

pub enum FilterMode {
    Find,
    Search,
}
