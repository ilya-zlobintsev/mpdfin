#![allow(clippy::useless_conversion)]
mod error;

use error::MediaKeysError;
use interoptopus::{
    callback, ffi_service, ffi_service_ctor, ffi_type, pattern, patterns::string::AsciiPointer,
    Inventory, InventoryBuilder,
};
use souvlaki::{MediaControlEvent, MediaControls, MediaMetadata, MediaPlayback, PlatformConfig};

#[ffi_type(opaque)]
pub struct MediaKeysService {
    media_controls: MediaControls,
}

callback!(CallbackMediaControlEvent(media_event: FFIMediaControlEvent));

#[ffi_service(error = "MediaKeysError", prefix = "media_keys_")]
impl MediaKeysService {
    #[ffi_service_ctor]
    pub fn new(name: AsciiPointer) -> Result<Self, MediaKeysError> {
        let name = name.as_str().unwrap();
        let platform_config = PlatformConfig {
            display_name: name,
            dbus_name: name,
            hwnd: None,
        };
        let media_controls = MediaControls::new(platform_config)?;
        Ok(Self { media_controls })
    }

    pub fn attach(&mut self, callback: CallbackMediaControlEvent) -> Result<(), MediaKeysError> {
        self.media_controls.attach(move |event| {
            if let Ok(ffi_event) = FFIMediaControlEvent::try_from(event) {
                callback.call(ffi_event);
            }
        })?;
        Ok(())
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

    pub fn set_playback(&mut self, playback: FFIMediaPlayback) -> Result<(), MediaKeysError> {
        self.media_controls.set_playback(playback.into())?;
        Ok(())
    }
}

// Unfortunately wrapping a pointer in an FFIOption produces invalid syntax in the c# generator
#[ffi_type]
#[repr(C)]
pub struct FFIMediaMetadata<'a> {
    pub title: AsciiPointer<'a>,
    pub album: AsciiPointer<'a>,
    pub artist: AsciiPointer<'a>,
    pub cover_url: AsciiPointer<'a>,
    // pub duration: Option<Duration>,
}

#[ffi_type]
#[repr(C)]
pub enum FFIMediaControlEvent {
    Play,
    Pause,
    Toggle,
    Next,
    Previous,
    Stop,
    Raise,
    Quit,
}

impl TryFrom<MediaControlEvent> for FFIMediaControlEvent {
    type Error = MediaKeysError;

    fn try_from(value: MediaControlEvent) -> Result<Self, Self::Error> {
        match value {
            MediaControlEvent::Play => Ok(Self::Play),
            MediaControlEvent::Pause => Ok(Self::Pause),
            MediaControlEvent::Toggle => Ok(Self::Toggle),
            MediaControlEvent::Next => Ok(Self::Next),
            MediaControlEvent::Previous => Ok(Self::Previous),
            MediaControlEvent::Stop => Ok(Self::Stop),
            MediaControlEvent::Raise => Ok(Self::Raise),
            MediaControlEvent::Quit => Ok(Self::Quit),
            _ => Err(MediaKeysError::OtherError),
        }
    }
}

#[ffi_type]
#[repr(C)]
pub enum FFIMediaPlayback {
    Stopped,
    Paused,
    Playing,
}

impl From<FFIMediaPlayback> for MediaPlayback {
    fn from(value: FFIMediaPlayback) -> Self {
        match value {
            FFIMediaPlayback::Stopped => Self::Stopped,
            FFIMediaPlayback::Paused => Self::Paused { progress: None },
            FFIMediaPlayback::Playing => Self::Playing { progress: None },
        }
    }
}

fn convert_ffi_optional_str(value: AsciiPointer<'_>) -> Option<&str> {
    value.as_str().ok().filter(|s| !s.is_empty())
}

pub fn my_inventory() -> Inventory {
    {
        InventoryBuilder::new()
            .register(pattern!(MediaKeysService))
            .inventory()
    }
}
