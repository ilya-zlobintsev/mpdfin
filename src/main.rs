mod config;
mod database;
mod mpd;
mod playback_server;

use anyhow::anyhow;
use config::Config;
use mpd::MpdServer;
use tracing_subscriber::{fmt::format::FmtSpan, EnvFilter};

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    tracing_subscriber::fmt()
        .with_env_filter(
            EnvFilter::try_from_default_env().unwrap_or_else(|_| EnvFilter::new("info")),
        )
        .with_span_events(FmtSpan::NEW | FmtSpan::CLOSE)
        .init();

    let config = Config::load_or_create()
        .await
        .map_err(|e| anyhow!("failed to load configuration: {}", e))?;
    tracing::trace!("Loaded config {:?}", config);

    let server = MpdServer::init(config).await?;

    server.run().await
}
