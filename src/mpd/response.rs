use super::Tag;
use crate::jellyfin::base::BaseItemDto;
use futures_lite::{AsyncWrite, AsyncWriteExt};
use std::fmt;
use std::io::{self, Write};
use strum::VariantArray;

pub struct Response {
    data: Vec<u8>,
}

impl Response {
    pub fn new() -> Self {
        Self {
            data: Vec::with_capacity(4),
        }
    }

    pub fn field(mut self, key: impl fmt::Display, value: impl fmt::Display) -> Self {
        self.add_field(key, value);
        self
    }

    pub fn repeated_field(mut self, key: impl fmt::Display, values: &[impl fmt::Display]) -> Self {
        self.add_repeated_field(key, values);
        self
    }

    pub fn add_field(&mut self, key: impl fmt::Display, value: impl fmt::Display) {
        writeln!(self.data, "{key}: {value}").unwrap();
    }

    pub fn add_repeated_field(&mut self, key: impl fmt::Display, values: &[impl fmt::Display]) {
        for value in values {
            self.add_field(&key, value)
        }
    }

    pub fn item(mut self, item: &BaseItemDto) -> Self {
        self.add_item(item);
        self
    }

    pub fn add_item(&mut self, item: &BaseItemDto) {
        self.add_field("file", &item.id);
        for tag in Tag::VARIANTS {
            if let Some(values) = item.get_tag_values(*tag) {
                self.add_repeated_field(tag, &values);
            }
        }

        if let Some(ticks) = item.run_time_ticks {
            // Convert ticks to seconds
            let duration = ticks as f64 / 10000000.0;
            self.add_field("duration", duration);
            self.add_field("time", duration as i64);
        }
    }

    pub fn extend(&mut self, other: &Self) {
        self.data.extend_from_slice(&other.data);
    }

    pub fn add_list_ok(&mut self) {
        writeln!(self.data, "list_OK").unwrap();
    }

    pub async fn write<W: AsyncWrite + Unpin>(mut self, w: &mut W) -> io::Result<()> {
        self.data.extend_from_slice(b"OK\n");
        w.write_all(&self.data).await
    }
}

impl Default for Response {
    fn default() -> Self {
        Self::new()
    }
}

impl fmt::Debug for Response {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let data = String::from_utf8_lossy(&self.data);
        let mut field_data = data.as_ref();
        if field_data.len() > 250 {
            field_data = &field_data[0..250];
        }
        f.debug_struct("Response")
            .field("data", &field_data)
            .finish()
    }
}
