use std::fmt::Display;

use serde::{de, ser, Serialize};

type Result<T> = std::result::Result<T, Error>;

pub struct Serializer {
    output: String,
}

pub fn to_string<T: Serialize>(value: &T) -> Result<String> {
    let mut serializer = Serializer {
        output: String::new(),
    };

    value.serialize(&mut serializer)?;

    Ok(serializer.output)
}

impl<'a> ser::Serializer for &'a mut Serializer {
    type Ok = ();

    type Error = Error;

    type SerializeSeq = Self;
    type SerializeTuple = Self;
    type SerializeTupleStruct = Self;
    type SerializeTupleVariant = Self;
    type SerializeMap = Self;
    type SerializeStruct = Self;
    type SerializeStructVariant = Self;

    fn serialize_bool(self, v: bool) -> Result<()> {
        self.output += if v { "1" } else { "0" };

        Ok(())
    }

    fn serialize_i8(self, v: i8) -> Result<()> {
        self.serialize_i64(i64::from(v))
    }

    fn serialize_i16(self, v: i16) -> Result<()> {
        self.serialize_i64(i64::from(v))
    }

    fn serialize_i32(self, v: i32) -> Result<()> {
        self.serialize_i64(i64::from(v))
    }

    fn serialize_i64(self, v: i64) -> Result<()> {
        self.output += &v.to_string();

        Ok(())
    }

    fn serialize_u8(self, v: u8) -> Result<()> {
        self.serialize_u64(u64::from(v))
    }

    fn serialize_u16(self, v: u16) -> Result<()> {
        self.serialize_u64(u64::from(v))
    }

    fn serialize_u32(self, v: u32) -> Result<()> {
        self.serialize_u64(u64::from(v))
    }

    fn serialize_u64(self, v: u64) -> Result<()> {
        self.output += &v.to_string();

        Ok(())
    }

    fn serialize_f32(self, v: f32) -> Result<()> {
        self.serialize_f64(f64::from(v))
    }

    fn serialize_f64(self, v: f64) -> Result<()> {
        self.output += &v.to_string();

        Ok(())
    }

    fn serialize_char(self, v: char) -> Result<()> {
        if v == ':' {
            Err(Error::ForbiddenCharacter(
                "Character \":\" is not allowed".to_owned(),
            ))
        } else {
            self.output.push(v);

            Ok(())
        }
    }

    fn serialize_str(self, v: &str) -> Result<()> {
        if v.split_whitespace().any(|w| w == "OK" || w == "ACK") {
            Err(Error::ForbiddenCharacter(
                "OK/ACK are not allowed in responses".to_owned(),
            ))
        } else {
            self.output += v;

            Ok(())
        }
    }

    fn serialize_bytes(self, _v: &[u8]) -> Result<()> {
        /*self.output += "binary: ";
        self.output += &v.len().to_string();


        self.output.as_bytes_mut() += v;

        Ok(())*/

        todo!()
    }

    fn serialize_none(self) -> Result<()> {
        Ok(())
    }

    fn serialize_some<T: ?Sized + Serialize>(self, value: &T) -> Result<()> {
        value.serialize(self)
    }

    fn serialize_unit(self) -> Result<()> {
        Ok(())
    }

    fn serialize_unit_struct(self, _name: &'static str) -> Result<()> {
        self.serialize_unit()
    }

    fn serialize_unit_variant(
        self,
        _name: &'static str,
        _variant_index: u32,
        variant: &'static str,
    ) -> Result<()> {
        self.serialize_str(variant)
    }

    fn serialize_newtype_struct<T: ?Sized + Serialize>(
        self,
        _name: &'static str,
        value: &T,
    ) -> Result<()> {
        value.serialize(self)
    }

    fn serialize_newtype_variant<T: ?Sized + Serialize>(
        self,
        _name: &'static str,
        _variant_index: u32,
        variant: &'static str,
        value: &T,
    ) -> Result<()> {
        variant.serialize(&mut *self)?;
        self.output += ": ";
        value.serialize(&mut *self)?;

        Ok(())
    }
    fn serialize_seq(self, _len: Option<usize>) -> Result<Self::SerializeSeq> {
        Ok(self)
    }

    fn serialize_tuple(self, len: usize) -> Result<Self::SerializeTuple> {
        if len == 2 {
            Ok(self)
        } else {
            Err(Error::Unsupported)
        }
    }

    fn serialize_tuple_struct(
        self,
        _name: &'static str,
        _len: usize,
    ) -> Result<Self::SerializeTupleStruct> {
        todo!()
    }

    fn serialize_tuple_variant(
        self,
        _name: &'static str,
        _variant_index: u32,
        _variant: &'static str,
        _len: usize,
    ) -> Result<Self::SerializeTupleVariant> {
        todo!()
    }

    fn serialize_map(self, _len: Option<usize>) -> Result<Self::SerializeMap> {
        Ok(self)
    }

    fn serialize_struct(self, _name: &'static str, len: usize) -> Result<Self::SerializeStruct> {
        self.serialize_map(Some(len))
    }

    fn serialize_struct_variant(
        self,
        _name: &'static str,
        _variant_index: u32,
        variant: &'static str,
        _len: usize,
    ) -> Result<Self::SerializeStructVariant> {
        variant.serialize(&mut *self)?;
        self.output += ": ";
        Ok(self)
    }
}

impl<'a> ser::SerializeSeq for &'a mut Serializer {
    // Must match the `Ok` type of the serializer.
    type Ok = ();
    // Must match the `Error` type of the serializer.
    type Error = Error;

    // Serialize a single element of the sequence.
    fn serialize_element<T>(&mut self, value: &T) -> Result<()>
    where
        T: ?Sized + Serialize,
    {
        if !(self.output.is_empty() || self.output.ends_with(": ") || self.output.ends_with("\n")) {
            self.output += "\n";
            if let Some(last_line) = self.output.lines().last().map(|s| s.to_string()) {
                if let Some(key) = last_line.split(':').next() {
                    self.output += key;
                    self.output += ": ";
                }
            }
        }
        value.serialize(&mut **self)?;
        Ok(())
    }

    // Close the sequence.
    fn end(self) -> Result<()> {
        Ok(())
    }
}

impl<'a> ser::SerializeTuple for &'a mut Serializer {
    type Ok = ();
    type Error = Error;
    fn serialize_element<T: ?Sized + Serialize>(&mut self, value: &T) -> Result<()> {
        if !self.output.is_empty() {
            self.output += ": ";
        }
        value.serialize(&mut **self)
    }

    fn end(self) -> Result<()> {
        if !self.output.ends_with("\n") {
            self.output += "\n";
        }
        Ok(())
    }
}

impl<'a> ser::SerializeTupleStruct for &'a mut Serializer {
    type Ok = ();
    type Error = Error;
    fn serialize_field<T: ?Sized + Serialize>(&mut self, _value: &T) -> Result<()> {
        todo!()
    }

    fn end(self) -> Result<()> {
        if !self.output.ends_with("\n") {
            self.output += "\n";
        }
        Ok(())
    }
}

impl<'a> ser::SerializeTupleVariant for &'a mut Serializer {
    type Ok = ();
    type Error = Error;
    fn serialize_field<T: ?Sized + Serialize>(&mut self, _value: &T) -> Result<()> {
        todo!()
    }

    fn end(self) -> Result<()> {
        if !self.output.ends_with("\n") {
            self.output += "\n";
        }
        Ok(())
    }
}

impl<'a> ser::SerializeMap for &'a mut Serializer {
    type Ok = ();
    type Error = Error;

    fn serialize_key<T: ?Sized + Serialize>(&mut self, key: &T) -> Result<()> {
        if !(self.output.is_empty() || self.output.ends_with('\n')) {
            self.output += "\n";
        }
        key.serialize(&mut **self)
    }

    fn serialize_value<T: ?Sized + Serialize>(&mut self, value: &T) -> Result<()> {
        self.output += ": ";
        value.serialize(&mut **self)?;
        Ok(())
    }

    fn end(self) -> Result<()> {
        if !self.output.ends_with("\n") {
            self.output += "\n";
        }
        Ok(())
    }
}

impl<'a> ser::SerializeStruct for &'a mut Serializer {
    type Ok = ();
    type Error = Error;

    fn serialize_field<T: ?Sized + Serialize>(
        &mut self,
        key: &'static str,
        value: &T,
    ) -> Result<()> {
        if !(self.output.is_empty() || self.output.ends_with('\n')) {
            self.output += "\n";
        }

        key.serialize(&mut **self)?;
        self.output += ": ";
        value.serialize(&mut **self)?;

        Ok(())
    }

    fn end(self) -> Result<()> {
        if !self.output.ends_with("\n") {
            self.output += "\n";
        }
        Ok(())
    }
}

impl<'a> ser::SerializeStructVariant for &'a mut Serializer {
    type Ok = ();
    type Error = Error;

    fn serialize_field<T: ?Sized + Serialize>(
        &mut self,
        key: &'static str,
        value: &T,
    ) -> Result<()> {
        key.serialize(&mut **self)?;
        self.output += ": ";
        value.serialize(&mut **self)?;

        Ok(())
    }

    fn end(self) -> Result<()> {
        if !self.output.ends_with("\n") {
            self.output += "\n";
        }
        Ok(())
    }
}

#[derive(Clone, Debug)]
pub enum Error {
    ForbiddenCharacter(String),
    Message(String),
    Unsupported,
}

impl ser::Error for Error {
    fn custom<T: Display>(msg: T) -> Self {
        Error::Message(msg.to_string())
    }
}

impl de::Error for Error {
    fn custom<T: Display>(msg: T) -> Self {
        Error::Message(msg.to_string())
    }
}

impl Display for Error {
    fn fmt(&self, formatter: &mut std::fmt::Formatter) -> std::fmt::Result {
        match self {
            Error::Message(msg) => formatter.write_str(msg),
            Error::ForbiddenCharacter(msg) => {
                formatter.write_str("forbidden character: ")?;
                formatter.write_str(msg)?;
                Ok(())
            }
            Error::Unsupported => formatter.write_str("unsupported"),
        }
    }
}

impl std::error::Error for Error {}

#[cfg(test)]
mod tests {
    use std::collections::{BTreeMap, HashMap};

    use crate::mpd::{
        model::{Decoder, DirectoryEntry, ListEntry, SongEntry},
        serializer::to_string,
    };

    #[test]
    fn serialize_seq() {
        let mut map = HashMap::new();
        map.insert("item", vec!["item1", "item2"]);
        let mpd_text = to_string(&map).unwrap();
        let expected_text = "item: item1\nitem: item2\n";
        assert_eq!(mpd_text, expected_text);
    }

    #[test]
    fn serialize_btreemap() {
        let mut map = BTreeMap::new();
        map.insert("value1", "response1");
        map.insert("value2", "response2");
        map.insert("value3", "response3");

        let mpd_text = to_string(&map).unwrap();

        assert_eq!(
            mpd_text,
            "value1: response1\nvalue2: response2\nvalue3: response3\n"
        )
    }

    #[test]
    fn serialize_entry_list() {
        let entries = vec![
            ListEntry::File(SongEntry {
                filename: "123".to_string(),
                title: Some("thissong".to_string()),
                album: Some("myalbum".to_string()),
                artist: Some("myartist".to_string()),
                composer: vec!["composer1".to_string(), "composer2".to_string()],
                genre: vec![],
                duration: 61.0,
            }),
            ListEntry::Directory(DirectoryEntry {
                directory: "somedir123".to_string(),
            }),
        ];

        let mpd_text = to_string(&entries).unwrap();

        let expected_text = format!(
            "file: 123
Artist: myartist
Album: myalbum
Title: thissong
Composer: composer1
Composer: composer2
Duration: 61
directory: somedir123
",
        );

        assert_eq!(mpd_text, expected_text);
    }

    #[test]
    fn serialize_decoders() {
        let decoders = vec![
            Decoder {
                plugin: "mad",
                suffix: "mp3",
                mime_type: vec!["audio/mpeg", "audio/somethingelse"],
            },
            Decoder {
                plugin: "mpcdec",
                suffix: "mpc",
                mime_type: vec![],
            },
        ];

        let mpd_text = to_string(&decoders).unwrap();

        let expected_text = "plugin: mad
suffix: mp3
mime_type: audio/mpeg
mime_type: audio/somethingelse
plugin: mpcdec
suffix: mpc
";

        assert_eq!(mpd_text, expected_text);
    }

    #[test]
    fn serialize_tuple() {
        let simple_tuple = ("key", "val");
        let mpd_text = to_string(&simple_tuple).unwrap();
        let expected = "key: val\n";
        assert_eq!(mpd_text, expected);
    }
}
