using System.Net;
using System.Net.Sockets;
using Mpdfin.Mpd;
using Serilog;

namespace Mpdfin;
static class Program
{
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
        catch (FileNotFoundException e)
        {
            Log.Error($"Could not load config file from {e.FileName}");
            return 1;
        }

        var auth = await Database.Authenticate(config.Jellyfin.ServerUrl, config.Jellyfin.Username, config.Jellyfin.Password);
        Log.Information($"Logged in as {auth.User.Name}");

        Database db = new(config.Jellyfin.ServerUrl, auth);
        _ = db.Update();

        Player.Player player = new();
        // CommandHandler commandHandler = new(player, db);

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
