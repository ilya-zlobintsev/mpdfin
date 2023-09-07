#![allow(clippy::useless_conversion)]
mod error;

use error::MediaKeysError;
use interoptopus::{
    ffi_service, ffi_service_ctor, ffi_type, pattern, patterns::string::AsciiPointer, Inventory,
    InventoryBuilder,
};
use souvlaki::{MediaControls, MediaMetadata, PlatformConfig};

#[ffi_type(opaque)]
pub struct MediaPlayerService {
    media_controls: MediaControls,
}

#[ffi_service(error = "MediaKeysError", prefix = "media_keys_")]
impl MediaPlayerService {
    #[ffi_service_ctor]
    pub fn new(name: AsciiPointer) -> Result<Self, MediaKeysError> {
        let name = name.as_str().unwrap();
        let platform_config = PlatformConfig {
            display_name: name,
            dbus_name: name,
            hwnd: None,
        };
        let mut media_controls = MediaControls::new(platform_config)?;

        media_controls
            .attach(|event| println!("Event received: {:?}", event))
            .unwrap();
        Ok(Self { media_controls })
    }

    pub fn set_metadata(&mut self, metadata: FFIMediaMetadata) -> Result<(), MediaKeysError> {
        let metadata = MediaMetadata {
            title: convert_ffi_optional_str(metadata.title),
            album: convert_ffi_optional_str(metadata.album),
            artist: convert_ffi_optional_str(metadata.artist),
            cover_url: convert_ffi_optional_str(metadata.cover_url),
            duration: None,
        };
        self.media_controls.set_metadata(metadata)?;
        Ok(())
    }
}

#[ffi_type]
#[repr(C)]
pub struct FFIMediaMetadata<'a> {
    pub title: AsciiPointer<'a>,
    pub album: AsciiPointer<'a>,
    pub artist: AsciiPointer<'a>,
    pub cover_url: AsciiPointer<'a>,
    // pub duration: Option<Duration>,
}

fn convert_ffi_optional_str(value: AsciiPointer<'_>) -> Option<&str> {
    value.as_str().ok().filter(|s| !s.is_empty())
}

pub fn my_inventory() -> Inventory {
    {
        InventoryBuilder::new()
            .register(pattern!(MediaPlayerService))
            .inventory()
    }
}
