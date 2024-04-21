mod connection;
mod error;
mod filters;
mod request;
mod response;
mod server;
pub(crate) mod subsystem;
mod tag;

pub use request::Request;
pub use server::Server;
pub use subsystem::Subsystem;
pub use subsystem::SubsystemNotifier;

use {error::Error, response::Response, tag::Tag};

type Result<T> = std::result::Result<T, Error>;

const MPD_VERSION: &str = "0.23.0";
