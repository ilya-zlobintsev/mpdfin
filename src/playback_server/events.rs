use souvlaki::MediaControlEvent;

use crate::mpd::model::Subsystem;

#[derive(Debug)]
pub enum MediaEvent {
    MediaControlEvent(MediaControlEvent),
    SubsystemUpdate(Subsystem),
}
