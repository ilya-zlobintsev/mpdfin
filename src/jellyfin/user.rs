use serde::{Deserialize, Serialize};

#[derive(Serialize)]
#[serde(rename_all = "PascalCase")]
pub struct AuthenticateUserByName {
    pub username: String,
    pub pw: String,
}

#[derive(Deserialize)]
#[serde(rename_all = "PascalCase")]
pub struct AuthenticationResult {
    pub access_token: String,
    pub server_id: String,
    pub user: UserDto,
    pub session_info: SessionInfo,
}

#[derive(Deserialize)]
#[serde(rename_all = "PascalCase")]
pub struct SessionInfo {
    pub id: Option<String>,
}

#[derive(Deserialize)]
#[serde(rename_all = "PascalCase")]
pub struct UserDto {
    pub id: String,
    pub name: Option<String>,
    pub server_id: Option<String>,
    pub server_name: Option<String>,
}
