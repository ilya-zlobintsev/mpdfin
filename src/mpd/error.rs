use std::num::ParseIntError;

use super::serializer;

#[derive(Debug)]
pub struct MpdError {
    pub cmd: Option<String>,
    pub code: Ack,
    pub msg: String,
}

impl MpdError {
    pub fn new(cmd: Option<String>, msg: String, code: Ack) -> Self {
        Self { cmd, msg, code }
    }

    pub fn wrong_argument_count(cmd: &str) -> Self {
        let msg = format!("wrong number of arguments for \"{}\"", cmd);
        Self::new(Some(cmd.to_string()), msg, Ack::Arg)
    }

    pub fn boolean_expected(cmd: &str) -> Self {
        Self::new(
            Some(cmd.to_string()),
            "boolean (0/1) expected".to_string(),
            Ack::Arg,
        )
    }

    pub fn integer_expeted(cmd: &str) -> Self {
        Self::new(
            Some(cmd.to_string()),
            "integer expected".to_string(),
            Ack::Arg,
        )
    }

    pub fn unknown_command(cmd: &str) -> Self {
        Self::new(None, format!("unknown command \"{}\"", cmd), Ack::Unknown)
    }
}

impl MpdError {
    pub fn to_string(&self, i: usize) -> String {
        format!(
            "ACK [{}@{}] {{{}}} {}\n",
            self.code as u32,
            i,
            self.cmd.as_deref().unwrap_or_default(),
            self.msg
        )
    }
}

#[derive(Debug, Clone, Copy)]
pub enum Ack {
    NotList = 1,
    Arg = 2,
    Password = 3,
    Permission = 4,
    Unknown = 5,
    NotFound = 50,
    PlaylistMax = 51,
    System = 52,
    PlaylistLoad = 53,
    UpdateAlready = 54,
    PlayerSync = 55,
    Exist = 56,
}

impl From<serializer::Error> for MpdError {
    fn from(e: serializer::Error) -> Self {
        Self::new(None, format!("serializer error: {}", e), Ack::System)
    }
}

impl From<ParseIntError> for MpdError {
    fn from(_e: ParseIntError) -> Self {
        Self::new(None, "expected an integer".to_string(), Ack::Arg)
    }
}

#[cfg(test)]
mod tests {
    use super::MpdError;

    #[test]
    fn unknown_command() {
        let error = MpdError::unknown_command("asdf");
        assert_eq!(
            error.to_string(0),
            "ACK [5@0] {} unknown command \"asdf\"\n"
        )
    }
}
