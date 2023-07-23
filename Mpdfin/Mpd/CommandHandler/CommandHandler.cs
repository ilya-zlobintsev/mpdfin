using Serilog;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    readonly Player.Player Player;
    readonly Database Db;
    public readonly ClientNotificationsReceiver NotificationsReceiver;

    public CommandHandler(Player.Player player, Database db)
    {
        Player = player;
        Db = db;
        NotificationsReceiver = new();

        Player.OnSubsystemUpdate += (e, args) => NotificationsReceiver.SendEvent(args.Subsystem);
        Db.OnUpdate += (e, args) => NotificationsReceiver.SendEvent(Subsystem.update);
        Db.OnDatabaseUpdated += (e, args) => NotificationsReceiver.SendEvent(Subsystem.database);
    }

    public Response HandleRequest(Request request)
    {
        Log.Debug($"Handling command {request.Command}");
        return request.Command switch
        {
            Command.ping => new(),
            Command.status => Status(),
            Command.currentsong => CurrentSong(),
            Command.play => Play(int.Parse(request.Args[0])),
            Command.playid => PlayId(request.Args[0]),
            Command.pause => Pause(request.Args.ElementAtOrDefault(0)),
            Command.getvol => GetVol(),
            Command.setvol => SetVol(int.Parse(request.Args[0])),
            Command.addid => AddId(request.Args[0]),
            Command.playlistinfo => PlaylistInfo(),
            Command.plchanges => PlChanges(int.Parse(request.Args[0])),
            Command.tagtypes => TagTypes(),
            _ => throw new NotImplementedException($"Command {request.Command} not implemented or cannot be called in the current context"),
        };
    }
}
