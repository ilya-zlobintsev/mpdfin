mod connection;
mod error;
mod filters;
mod request;
pub mod response;
pub mod server;
pub mod subsystem;
mod tag;

// pub use filters::FilterExpression;
// pub use request::Request;
// pub use response::Response;
// pub use tag::Tag;

pub use request::Request;
pub use subsystem::Subsystem;

use {error::Error, response::Response, tag::Tag};

type Result<T> = std::result::Result<T, Error>;

const MPD_VERSION: &str = "0.23.0";
