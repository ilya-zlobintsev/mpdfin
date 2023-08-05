using System.Net;
using System.Net.Sockets;
using Mpdfin.Mpd;
using Mpdfin.DB;
using Serilog;
using System.Diagnostics.CodeAnalysis;
using Mpdfin.Player;

namespace Mpdfin;
static class Program
{
    [RequiresUnreferencedCode("Serialization")]
    private static async Task<int> Main()
    {
        using var log = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "[{Level:u}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        Log.Logger = log;

        Config config;
        try
        {
            config = Config.Load();
            Log.Debug($"Loaded config {config}");
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            Log.Error($"Could not load config file: {e.Message}");
            return 1;
        }

        Database db;

        var storage = DatabaseStorage.Load();
        if (storage is null)
        {
            Log.Information("Database does not exist");
            var auth = await Database.Authenticate(config.Jellyfin.ServerUrl, config.Jellyfin.Username, config.Jellyfin.Password);
            Log.Information($"Logged in as {auth.User.Name}");
            storage = new(auth);

            db = new(config.Jellyfin.ServerUrl, storage);

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
            db = new(config.Jellyfin.ServerUrl, storage);
            Log.Information($"Loaded database with {storage.Items.Count} items");
        }

        Player.Player player;

        var state = PlayerState.Load();
        if (state is not null)
        {
            player = new(state, db);
        }
        else
        {
            player = new();
        }

        player.OnSubsystemUpdate += (_, _) => _ = player.State.Save();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => player.State.Save().Wait();
        Console.CancelKeyPress += (_, _) => player.State.Save().Wait();

        IPEndPoint ipEndPoint = new(IPAddress.Any, 6601);
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
