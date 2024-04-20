use std::{error::Error, fmt};

#[derive(Debug)]
pub enum JellyfinError {
    Generic { status: u16, text: Option<String> },
    Request(Box<ureq::Error>),
    Io(std::io::Error),
}

impl From<ureq::Error> for JellyfinError {
    fn from(err: ureq::Error) -> Self {
        Self::Request(Box::new(err))
    }
}

impl From<std::io::Error> for JellyfinError {
    fn from(err: std::io::Error) -> Self {
        Self::Io(err)
    }
}

impl fmt::Display for JellyfinError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            JellyfinError::Generic { status, text } => {
                write!(f, "Jellyfin return error {status}: {text:?}")
            }
            JellyfinError::Request(err) => write!(f, "Request error: {err}"),
            JellyfinError::Io(err) => write!(f, "IO error: {err}"),
        }
    }
}

impl Error for JellyfinError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        None
        // match self {
        //     JellyfinError::Generic { .. } => None,
        //     JellyfinError::Request(err) => Some(&err),
        // }
    }
}
