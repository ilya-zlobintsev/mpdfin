use std::fmt;

#[derive(Debug)]
pub enum Error {
    UnknownCommand(String),
    Io,
    InvalidArg(String),
}

impl Error {
    pub fn ack(&self) -> Ack {
        match self {
            Error::UnknownCommand(_) => Ack::Unknown,
            Error::Io => Ack::System,
            Error::InvalidArg(_) => Ack::Arg,
        }
    }

    pub fn to_response(&self, command_list_num: usize, current_command: Option<&str>) -> String {
        let ack = self.ack() as u16;
        let current_command = current_command.unwrap_or_default();

        format!("ACK [{ack}@{command_list_num}] {{{current_command}}} {self}\n")
    }
}

impl fmt::Display for Error {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Error::UnknownCommand(_) => write!(f, "Unknown command"),
            Error::Io => write!(f, "IO error"),
            Error::InvalidArg(text) => write!(f, "{text}"),
        }
    }
}

impl std::error::Error for Error {}

impl From<std::io::Error> for Error {
    fn from(_: std::io::Error) -> Self {
        Self::Io
    }
}

pub enum Ack {
    NotList = 1,
    Arg = 2,
    Password = 3,
    Permission = 4,
    Unknown = 5,
    NoExist = 50,
    PlaylistMax = 51,
    System = 52,
    PlaylistLoad = 53,
    UpdateAlready = 54,
    PlayerSync = 55,
    Exist = 56,
}

#[cfg(test)]
mod tests {
    use super::Error;

    #[test]
    fn example_bad_song_index() {
        let error = Error::InvalidArg("Bad song index".to_owned());
        let response = error.to_response(1, Some("play"));
        assert_eq!("ACK [2@1] {play} Bad song index\n", response);
    }
}
