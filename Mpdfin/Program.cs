using System.Net;
using System.Net.Sockets;
using Mpdfin.Mpd;
using Mpdfin.DB;
using Serilog;
using System.Diagnostics.CodeAnalysis;
using Mpdfin.Player;
using Serilog.Events;
using Jellyfin.Sdk;
using Mpdfin.Interop;

namespace Mpdfin;
static class Program
{
    [RequiresUnreferencedCode("Serialization")]
    private static async Task<int> Main()
    {
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

        using var log = new LoggerConfiguration()
            .MinimumLevel.Is(config.LogLevel ?? LogEventLevel.Information)
            .WriteTo.Console(outputTemplate: "[{Level:u}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        Log.Logger = log;

        Database db;
        JellyfinClient client;

        var storage = DatabaseStorage.Load();
        if (storage is null)
        {
            Log.Information("Database does not exist");
            var auth = await JellyfinClient.Authenticate(config.Jellyfin.ServerUrl, config.Jellyfin.Username, config.Jellyfin.Password);
            Log.Information($"Logged in as {auth.User.Name}");

            storage = new(auth);
            client = new(config.Jellyfin.ServerUrl, auth);
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
            client = new(config.Jellyfin.ServerUrl, storage.AuthenticationResult);
            db = new(client, storage);
            Log.Information($"Loaded database with {storage.Items.Count} items");
        }


        Player.Player player = new();

        player.OnPlaybackStarted += async (_, _) =>
        {
            Log.Debug("Playback started");
            var currentSong = player.CurrentSong;
            if (currentSong is not null)
            {
                PlaybackStartInfo info = new()
                {
                    ItemId = player.CurrentSong!.Value.Item.Id
                };

                await client.PlaystateClient.ReportPlaybackStartAsync(info);
                Log.Debug("Reported playback start");
            }
        };

        var state = PlayerState.Load();
        if (state is not null)
        {
            player.LoadState(state, db);
        }

        var mediaPlayerService = MediaPlayerService.New("mpdfin");
        FFIMediaMetadata metadata = new()
        {
            title = "asd",
            artist = "test"
        };
        mediaPlayerService.SetMetadata(metadata);
        // mediaKeysController

        var lastStateUpdate = DateTime.MinValue;
        player.OnSubsystemUpdate += (_, _) => Task.Run(async () =>
        {
            if ((DateTime.UtcNow - lastStateUpdate) > TimeSpan.FromSeconds(10))
            {
                await player.State.Save();
                lastStateUpdate = DateTime.UtcNow;
            }
        });
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
    }

    [RequiresUnreferencedCode("Serialization")]
    async static Task HandleStream(TcpClient client, Player.Player player, Database db)
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
}
