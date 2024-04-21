pub mod database;
pub mod playback;
pub mod playlist;
pub mod status;

use super::Server;
use crate::mpd::subsystem::SubsystemListener;
use async_net::TcpStream;
use futures_lite::io::BufReader;

pub(super) struct CommandContext<'a> {
    pub server: &'a Server,
    pub stream: &'a mut BufReader<TcpStream>,
    pub args: Vec<String>,
    pub subsystem_listener: &'a mut SubsystemListener,
}
