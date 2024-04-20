pub mod base;
mod error;
pub mod items;
pub mod user;

pub use error::JellyfinError;

use self::{
    base::ItemsResponse,
    items::ItemsQuery,
    user::{AuthenticateUserByName, AuthenticationResult},
};
use serde::{de::DeserializeOwned, Serialize};
use std::{fmt, rc::Rc, time::Duration};

type Result<T> = std::result::Result<T, JellyfinError>;

#[derive(Clone)]
pub struct JellyfinClient {
    agent: ureq::Agent,
    base_url: Rc<str>,
    pub settings: Rc<ClientSettings>,
}

impl JellyfinClient {
    pub fn new(base_url: String, settings: ClientSettings) -> Self {
        let agent = ureq::AgentBuilder::new()
            .timeout_read(Duration::from_secs(15))
            .timeout_write(Duration::from_secs(15))
            .build();

        Self {
            agent,
            base_url: base_url.into(),
            settings: Rc::new(settings),
        }
    }

    fn get_request<R: DeserializeOwned>(&self, path: &str, query: impl Serialize) -> Result<R> {
        let query = serde_urlencoded::to_string(query).unwrap();

        let mut url = format!("{}{path}", self.base_url);
        if !query.is_empty() {
            url.push('?');
            url.push_str(&query);
        }

        let response = self
            .agent
            .get(&url)
            .set("Authorization", &self.settings.to_string())
            .call()?;

        // let response = self.client.get(url).query(&query).send()?;
        if response.status() / 100 == 2 {
            Ok(response.into_json()?)
        } else {
            let status = response.status();
            let text = response.into_string().ok();
            Err(JellyfinError::Generic { status, text })
        }
    }

    fn post_request<R: DeserializeOwned>(&self, path: &str, payload: impl Serialize) -> Result<R> {
        let url = format!("{}{path}", self.base_url);
        let response = self
            .agent
            .post(&url)
            .set("Authorization", &self.settings.to_string())
            .send_json(payload)?;
        if response.status() / 100 == 2 {
            Ok(response.into_json()?)
        } else {
            let status = response.status();
            let text = response.into_string().ok();
            Err(JellyfinError::Generic { status, text })
        }
    }

    pub fn authenticate_by_name(
        &self,
        req: AuthenticateUserByName,
    ) -> Result<AuthenticationResult> {
        self.post_request("/Users/AuthenticateByName", req)
    }

    pub fn get_user_views(&self, user_id: &str) -> Result<ItemsResponse> {
        self.get_request(&format!("/Users/{user_id}/Views"), Vec::<String>::new())
    }

    pub fn get_user_items(&self, user_id: &str, query: ItemsQuery) -> Result<ItemsResponse> {
        self.get_request(&format!("/Users/{user_id}/Items"), query)
    }

    pub fn get_audio_stream_url(
        &self,
        item_id: &str,
        // current_user_id: &str,
    ) -> String {
        let token = self.settings.token.as_ref().expect("Missing token");
        format!("{}/Audio/{item_id}/universal?api_key={token}&Container=opus,webm|opus,mp3,aac,m4a|aac,m4b|aac,flac,webma,webm|webma,wav,ogg", self.base_url)
    }
}

#[derive(Clone)]
pub struct ClientSettings {
    client_name: String,
    client_version: String,
    device_name: String,
    device_id: String,
    pub token: Option<String>,
}

impl ClientSettings {
    pub fn new(token: Option<String>) -> Self {
        Self {
            client_name: env!("CARGO_PKG_NAME").to_owned(),
            client_version: env!("CARGO_PKG_VERSION").to_owned(),
            device_name: "test".to_owned(),
            device_id: "123".to_owned(),
            token,
        }
    }
}

impl fmt::Display for ClientSettings {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            f,
            "MediaBrowser Client=\"{}\", Version=\"{}\", Device=\"{}\", DeviceId=\"{}\"",
            self.client_name, self.client_version, self.device_name, self.device_id
        )?;

        if let Some(token) = &self.token {
            write!(f, ", Token=\"{token}\"")?;
        }

        Ok(())
    }
}
