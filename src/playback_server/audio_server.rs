use libmpv::Mpv;
use std::sync::{Arc, RwLock};

#[derive(Clone)]
pub struct AudioServer {
    mpv: Arc<Mpv>,
    current_file: Arc<RwLock<String>>,
}

impl AudioServer {
    pub fn new() -> Self {
        let mpv = Mpv::new().expect("Couldn't initialize mpv");
        mpv.set_property("vo", "null")
            .expect("Couldn't set vo=null in libmpv");

        Self {
            mpv: Arc::new(mpv),
            current_file: Arc::new(RwLock::new(String::new())),
        }
    }

    pub fn play_file(&self, file_path: &str) {
        let args = [&format!("\"{}\"", file_path), "replace"];
        
        let mut current_file_guard = self.current_file.write().unwrap();

        if *current_file_guard != file_path {
            self.mpv.command("loadfile", &args).expect("Failed to play");
            *current_file_guard = file_path.to_string();
        }
    }

    pub fn unpause(&self) {
        self.mpv.unpause().expect("Failed to unpause");
    }

    pub fn pause(&self) {
        self.mpv.pause().expect("Failed to pause")
    }

    pub fn get_volume(&self) -> i64 {
        self.mpv
            .get_property("volume")
            .expect("Failed to get volume")
    }
}
