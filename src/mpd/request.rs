use super::error::Error;
use crate::mpd::Result;

#[derive(Debug, PartialEq, Eq)]
pub struct Request<'a> {
    pub command: &'a str,
    pub args: Vec<String>,
}

impl<'a> Request<'a> {
    pub fn parse(input: &'a str) -> Result<Self> {
        match input.split_once(|c: char| c == ' ' || c == '\t') {
            Some((command, raw_args)) => {
                let mut args = Vec::with_capacity(1);

                let mut current_arg = String::new();
                let mut chars = raw_args.chars();

                while let Some(current) = chars.next() {
                    match current {
                        ' ' | '\t' => {
                            args.push(current_arg);
                            current_arg = String::new();
                        }
                        '\\' => {
                            let escaped_char = chars.next().ok_or_else(|| {
                                Error::InvalidArg("EOL after escape character".to_owned())
                            })?;
                            current_arg.push(escaped_char);
                        }
                        '"' => {
                            current_arg.clear();
                            while let Some(current) = chars.next() {
                                match current {
                                    '"' => {
                                        chars.next(); // Consume the whitespace after quote if it's present
                                        break;
                                    }
                                    '\\' => {
                                        let escaped_char = chars.next().ok_or_else(|| {
                                            Error::InvalidArg(
                                                "EOL after escape character".to_owned(),
                                            )
                                        })?;
                                        current_arg.push(escaped_char);
                                    }
                                    _ => current_arg.push(current),
                                }
                            }

                            args.push(current_arg);
                            current_arg = String::new();
                            continue;
                        }
                        _ => current_arg.push(current),
                    }
                }
                if !current_arg.is_empty() {
                    args.push(current_arg);
                }

                Ok(Self { command, args })
            }
            None => Ok(Self {
                command: input,
                args: vec![],
            }),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::Request;

    #[test]
    fn parse_basic() {
        let request = Request::parse("ping").unwrap();
        assert_eq!(
            Request {
                command: "ping",
                args: vec![],
            },
            request
        );
    }

    #[test]
    fn parse_simple_args() {
        let request = Request::parse("play 1 2 3").unwrap();
        assert_eq!(
            Request {
                command: "play",
                args: vec!["1".to_owned(), "2".to_owned(), "3".to_owned()],
            },
            request
        );
    }

    #[test]
    fn parse_trailing_space_in_args() {
        let request = Request::parse("play 1 2 ").unwrap();
        assert_eq!(vec!["1", "2"], request.args);
    }

    #[test]
    fn parse_empty_arg_in_quotes() {
        let request = Request::parse(r#"play 1 2 """#).unwrap();
        assert_eq!(vec!["1", "2", ""], request.args);
    }

    #[test]
    fn parse_quoted_arg_with_space() {
        let request = Request::parse("play \"1 2\"").unwrap();
        assert_eq!(vec!["1 2"], request.args);
    }

    #[test]
    fn parse_escaped_quotes_in_args() {
        let request = Request::parse("find \"multi \\\"word\\\" arg\"").unwrap();
        assert_eq!(vec!["multi \"word\" arg"], request.args);
    }

    #[test]
    fn parse_arg_after_quote() {
        let request = Request::parse("foobar \"multi \\\"word\\\" arg\" 123").unwrap();
        assert_eq!(vec!["multi \"word\" arg", "123"], request.args);
    }

    #[test]
    fn parse_quotes_in_filter() {
        let request = Request::parse(r#"find "(Artist == \"foo\\'bar\\\"\")""#).unwrap();
        assert_eq!(r#"(Artist == "foo\'bar\"")"#, request.args[0]);
    }
}
