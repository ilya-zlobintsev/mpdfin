use super::{error::Error, Result};
use event_listener::{Event, EventListener};
use futures_lite::{Future, FutureExt};
use log::trace;
use std::{
    pin::Pin,
    str::FromStr,
    sync::Arc,
    task::{Context, Poll},
};
use strum::{Display, EnumString, FromRepr, VariantArray};

#[derive(Display, EnumString, PartialEq, Eq, Hash, Clone, Copy, Debug, FromRepr, VariantArray)]
#[strum(serialize_all = "snake_case")]
pub enum Subsystem {
    Database = 0,
    Update,
    StoredPlaylist,
    Playlist,
    Player,
    Mixer,
    Output,
    Options,
    Partition,
    Sticker,
    Subscription,
    Message,
    Neighbor,
    Mount,
}

impl Subsystem {
    /// [`from_str`] with the mpd error
    pub fn try_from_str(value: &str) -> Result<Self> {
        Self::from_str(value).map_err(|_| Error::InvalidArg(format!("Unknown subsystem '{value}'")))
    }
}

// The index of the inner slice is the enum discriminant
#[derive(Clone)]
pub struct SubsystemNotifier(Arc<[Event]>);

impl SubsystemNotifier {
    pub fn new() -> Self {
        let subsystem_events: Vec<Event> =
            Subsystem::VARIANTS.iter().map(|_| Event::new()).collect();
        Self(subsystem_events.into())
    }

    pub fn notify(&self, subsystem: Subsystem) {
        let notified = self.0[subsystem as usize].notify(usize::MAX);
        trace!("Notified {notified} listeners about a change in '{subsystem}'");
    }

    pub fn listener(&self) -> SubsystemListener {
        let listeners = self.0.iter().map(|event| event.listen()).collect();
        SubsystemListener {
            listeners,
            notifier: self.clone(),
        }
    }
}

impl Default for SubsystemNotifier {
    fn default() -> Self {
        Self::new()
    }
}

pub struct SubsystemListener {
    // The index of the inner slice is the enum discriminant (same as in Notifier)
    listeners: Box<[EventListener]>,
    notifier: SubsystemNotifier,
}

impl SubsystemListener {
    pub fn listen<'a>(&'a mut self, subsystems: &'a [Subsystem]) -> ListenSubsystems<'a> {
        ListenSubsystems {
            listener: self,
            subsystems,
        }
    }
}

pub struct ListenSubsystems<'a> {
    listener: &'a mut SubsystemListener,
    subsystems: &'a [Subsystem],
}

impl<'a> Future for ListenSubsystems<'a> {
    type Output = Vec<Subsystem>;

    fn poll(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Self::Output> {
        let this = self.get_mut();

        let mut changed = Vec::new();

        for subsystem in this.subsystems {
            let subsystem = *subsystem;
            let listener = &mut this.listener.listeners[subsystem as usize];

            if listener.poll(cx).is_ready() {
                *listener = this.listener.notifier.0[subsystem as usize].listen();
                changed.push(subsystem);
            }
        }

        if changed.is_empty() {
            Poll::Pending
        } else {
            Poll::Ready(changed)
        }
    }
}

#[cfg(test)]
mod tests {
    use super::{Subsystem, SubsystemNotifier};
    use futures_lite::future;
    use strum::VariantArray;

    #[test]
    fn listen_all_subsystems() {
        let notifier = SubsystemNotifier::new();
        let mut listener = notifier.listener();

        notifier.notify(Subsystem::Database);

        let notified_subsystem = future::block_on(listener.listen(Subsystem::VARIANTS));
        assert_eq!(vec![Subsystem::Database], notified_subsystem);
    }

    #[test]
    fn listern_subsystems_filtered() {
        let notifier = SubsystemNotifier::new();
        let mut listener = notifier.listener();

        let listener_handle =
            std::thread::spawn(move || future::block_on(listener.listen(&[Subsystem::Options])));

        notifier.notify(Subsystem::Database);
        assert!(!listener_handle.is_finished());

        notifier.notify(Subsystem::Options);
        let received = listener_handle.join().unwrap();
        assert_eq!(vec![Subsystem::Options], received);
    }

    #[test]
    fn listen_multiple_subsystems() {
        let notifier = SubsystemNotifier::new();
        let mut listener = notifier.listener();

        notifier.notify(Subsystem::Database);
        notifier.notify(Subsystem::Mixer);
        notifier.notify(Subsystem::Options);

        let notified = future::block_on(listener.listen(&[Subsystem::Mixer, Subsystem::Options]));
        assert_eq!(vec![Subsystem::Mixer, Subsystem::Options], notified);
    }
}
