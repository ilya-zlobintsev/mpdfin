using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Jellyfin.Sdk;
using Serilog;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    readonly Player.Player Player;
    readonly Database Db;
    public readonly ClientNotificationsReceiver NotificationsReceiver;
    readonly ClientStream ClientStream;

    public CommandHandler(Player.Player player, Database db, ClientStream clientStream)
    {
        Player = player;
        Db = db;
        ClientStream = clientStream;
        NotificationsReceiver = new();

        Player.OnSubsystemUpdate += (e, args) => NotificationsReceiver.SendEvent(args.Subsystem);
        Db.OnUpdate += (e, args) => NotificationsReceiver.SendEvent(Subsystem.update);
        Db.OnDatabaseUpdated += (e, args) => NotificationsReceiver.SendEvent(Subsystem.database);
    }

    public async Task HandleStream()
    {
        Request? request;
        while ((request = await ClientStream.ReadRequest()) is not null)
        {
            try
            {
                var response = await HandleRequest(request.Value);

                if (!ClientStream.EndOfStream && response is not null)
                {
                    await ClientStream.WriteResponse(response.Value);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Error occured when processing request `{request}`: {ex}");
                await ClientStream.WriteError(Ack.UNKNOWN, messageText: ex.Message);
            }
        }
    }

    async Task<Response?> HandleRequest(Request request)
    {
        var methods = GetType().GetMethods();
        var commandMethods = Array.FindAll(methods, method => method.Name.ToLower() == request.Command)
            ?? throw new NotImplementedException($"Command `{request.Command}` not recognized");
        // Find the right overload
        var commandMethod = Array.Find(commandMethods, method => method.GetParameters().Length == request.Args.Count) ?? throw new Exception("Invalid number of arguments provided");

        var obj = commandMethod.Invoke(this, request.Args.ToArray());

        if (obj is Task<Response> task)
        {
            return await task;
        }
        else if (obj is Response response)
        {
            return response;
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    /*async Task<Response> HandleCommandList(ClientStream stream, bool printOk)
    {
        List<Request> requestList = new();

        bool end = false;
        Log.Debug("Processing command list");
        Request? request;
        while ((request = await stream.ReadRequest()) is not null)
        {
            switch (request.Value.Command)
            {
                case Command.command_list_end:
                    Log.Debug("Exiting command list");
                    end = true;
                    break;
                default:
                    Log.Debug($"Queueing command {request.Value.Command}");
                    requestList.Add(request.Value);
                    break;
            }

            if (end)
            {
                break;
            }
        }

        Response totalResponse = new();

        foreach (var queuedRequest in requestList)
        {
            var response = await HandleRequest(queuedRequest, stream);
            totalResponse.Extend(response.Value);

            if (printOk)
            {
                totalResponse.AddListOk();
            }
        }

        return totalResponse;
    }*/
}
