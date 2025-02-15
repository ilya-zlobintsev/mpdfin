using System.Net;
using System.Net.Sockets;
using Mpdfin.Mpd;
using Mpdfin.DB;
using Serilog;
using Mpdfin.Player;
using Serilog.Events;
using Mpdfin;

Config config;
try
{
    config = Config.Load();
}
catch (Exception e)
{
    Console.WriteLine($"Could not load config file: {e.Message}");
    throw;
}

var jellyfinConfig = config.Jellyfin;

var logLevel = config.LogLevel is not null ? Enum.Parse<LogEventLevel>(config.LogLevel, true) : LogEventLevel.Information;
await using var log = new LoggerConfiguration()
    .MinimumLevel.Is(logLevel)
    .WriteTo.Console(outputTemplate: "[{Level:u}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

Log.Logger = log;

Database db;
JellyfinClient client;

var auth = await JellyfinClient.Authenticate(jellyfinConfig.ServerUrl, jellyfinConfig.Username, jellyfinConfig.Password);
Log.Information($"Logged in as {auth!.User!.Name}");

var storage = DatabaseStorage.Load();
if (storage is null)
{
    Log.Information("Database does not exist");

    storage = new(auth);
    client = new(jellyfinConfig.ServerUrl, auth);
    db = new(client, storage);

    try
    {
        await db.Update();
    }
    catch (Exception ex)
    {
        Log.Error(ex.ToString());
    }
}
else
{
    client = new(jellyfinConfig.ServerUrl, auth);
    db = new(client, storage);
    Log.Information($"Loaded database with {storage.Items.Count} items");
}

Player player = new(db);

// player.OnPlaybackStarted += async (_, _) =>
// {
//     Log.Debug("Playback started");
//     var currentSong = player.CurrentSong;
//     if (currentSong is not null)
//     {
//         PlaybackStartInfo info = new()
//         {
//             ItemId = player.CurrentSong!.SongId,
//         };

//         await client.PlaystateClient.ReportPlaybackStartAsync(info);
//         Log.Debug("Reported playback start");
//     }
// };

var state = PlayerState.Load();
if (state is not null)
{
    player.LoadState(state, db);
}

var lastStateUpdate = DateTime.MinValue;
player.OnSubsystemUpdate += (_, _) => Task.Run(async () =>
{
    if ((DateTime.UtcNow - lastStateUpdate) > TimeSpan.FromSeconds(10))
    {
        await player.State.Save();
        lastStateUpdate = DateTime.UtcNow;
    }
});

// TODO: Consider replacing with PosixSignalRegistration
AppDomain.CurrentDomain.ProcessExit += (_, _) => player.State.Save().Wait();
Console.CancelKeyPress += (_, _) => player.State.Save().Wait();

IPEndPoint ipEndPoint = new(IPAddress.Any, config.Port ?? 6600);
TcpListener listener = new(ipEndPoint);

try
{
    listener.Start();
    Log.Information($"Listening on {ipEndPoint}");
    while (true)
    {
        Log.Debug("Waiting for new connection");
        TcpClient handler = await listener.AcceptTcpClientAsync();
        _ = HandleStream(handler, player, db);
    }
}
finally
{
    listener.Stop();
}

static async Task HandleStream(TcpClient client, Player player, Database db)
{
    try
    {
        CommandHandler commandHandler = new(player, db);

        await using ClientStream stream = new(client);
        await stream.WriteGreeting();

        await commandHandler.HandleStream(stream);
    }
    catch (Exception ex)
    {
        Log.Error(ex.ToString());
        throw;
    }
}
