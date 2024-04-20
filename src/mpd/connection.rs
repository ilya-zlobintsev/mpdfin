// use super::{request::Request, Result};
// use std::io::{BufReader, Read, Write};

// pub struct ClientConnection<T> {
//     inner: BufReader<T>,
// }

// impl<T: Read + Write> ClientConnection<T> {
//     pub fn new(inner: T) -> Self {
//         Self {
//             inner: BufReader::new(inner),
//         }
//     }

//     pub async fn read_request(&mut self) -> Result<Option<Request>> {
//         todo!();
//         // let mut buf = String::new();
//         // if self.inner.read_line(&mut buf).await? != 0 {
//         //     let request = Request::parse(&buf)?;
//         //     Ok(Some(request))
//         // } else {
//         //     Ok(None)
//         // }
//     }
// }
