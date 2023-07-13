using System.Net;
using System.Net.Sockets;
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

        Player player = new();
        CommandHandler commandHandler = new(player, db);

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
                _ = HandleStream(handler, commandHandler);
            }
        }
        finally
        {
            listener.Stop();
        }

        async static Task HandleStream(TcpClient client, CommandHandler commandHandler)
        {
            // For some reason `await foreach` doesn't yield back
            await Task.Yield();

            await using ClientStream stream = new(client);
            await stream.WriteGreeting();

            var requests = stream.ReadCommands();

            await foreach (var request in requests)
            {
                try
                {
                    var response = request.Command switch
                    {
                        Command.command_list_begin => await HandleCommandList(requests, commandHandler, false),
                        Command.command_list_ok_begin => await HandleCommandList(requests, commandHandler, true),
                        _ => commandHandler.HandleRequest(request),
                    };
                    await stream.WriteResponse(response);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }
            }
        }

        async static Task<Response> HandleCommandList(IAsyncEnumerable<Request> incomingRequests, CommandHandler handler, bool printOk)
        {
            List<Request> requestList = new();

            bool end = false;
            Log.Debug("Processing command list");
            await foreach (var request in incomingRequests)
            {
                switch (request.Command)
                {
                    case Command.command_list_end:
                        Log.Debug("Exiting command list");
                        end = true;
                        break;
                    default:
                        Log.Debug($"Queueing command {request.Command}");
                        requestList.Add(request);
                        break;
                }

                if (end)
                {
                    break;
                }
            }

            Response totalResponse = new();

            foreach (var request in requestList)
            {
                var response = handler.HandleRequest(request);
                totalResponse.Extend(response);

                if (printOk)
                {
                    totalResponse.AddListOk();
                }
            }

            return totalResponse;
        }
    }
}
