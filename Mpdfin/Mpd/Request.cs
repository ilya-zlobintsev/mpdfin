using U8.Abstractions;

namespace Mpdfin.Mpd;

public enum Command
{
    ping,

    status,
    currentsong,
    play,
    playid,
    pause,
    stop,
    getvol,
    setvol,
    volume,
    seek,
    seekid,
    seekcur,
    next,
    previous,
    add,
    addid,
    delete,
    deleteid,
    clear,
    random,
    playlistinfo,
    plchanges,
    find,
    tagtypes,
    idle,
    noidle,
    list,
    lsinfo,
    outputs,
    stats,
    commands,
    decoders,
    update,
    shuffle,
    replay_gain_status,
    listplaylists,

    command_list_begin,
    command_list_ok_begin,
    command_list_end,
}

public readonly record struct Request
{
    public readonly Command Command;
    public readonly List<U8String> Args;

    static Command ParseCommand(U8String rawCommand)
    {
        return !int.TryParse(rawCommand, out int _)
            && U8Enum.TryParse(rawCommand, out Command command)
                ? command : throw new Exception($"unknown command {rawCommand}");
    }

    public Request(U8String raw)
    {
        (var commandValue, raw) = raw.SplitFirst(' ');

        Command = ParseCommand(commandValue);

        Args = [];

        var argBuilder = new InlineU8Builder();
        var runes = raw.Runes.GetEnumerator();

        while (runes.MoveNext())
        {
            var c = runes.Current;
            switch (c.Value)
            {
                case '"':
                    if (argBuilder.BytesWritten > 0)
                        throw new($"Unexpected data before quote: {u8(ref argBuilder)}");

                    var exitLoop = false;
                    while (runes.MoveNext() && !exitLoop)
                    {
                        var innerC = runes.Current;
                        switch (innerC.Value)
                        {
                            case '"':
                                Args.Add(u8(ref argBuilder));
                                exitLoop = true;
                                break;
                            case '\\':
                                if (!runes.MoveNext()) throw new("No character after escape symbol");

                                argBuilder.AppendFormatted(runes.Current);
                                break;
                            default:
                                argBuilder.AppendFormatted(innerC);
                                break;
                        }
                    }
                    break;
                case ' ':
                    if (argBuilder.BytesWritten > 0)
                    {
                        Args.Add(u8(ref argBuilder));
                    }
                    break;
                case '\\':
                    if (!runes.MoveNext()) throw new("No character after escape symbol");

                    argBuilder.AppendFormatted(runes.Current);
                    break;
                default:
                    argBuilder.AppendFormatted(c);
                    break;
            }
        }

        if (argBuilder.BytesWritten > 0)
        {
            Args.Add(u8(ref argBuilder));
        }
    }

    public override string ToString()
    {
        return $"{Command} {string.Join(" ", Args)}";
    }

    public static Range ParseRange(U8String input)
    {
        var (startValue, endValue) = input.SplitFirst(':');

        var result =
            int.TryParse(startValue, out var start) &
            int.TryParse(endValue, out var end);

        return result ? new(start, end) : throw new FormatException($"Invalid range {input}");
    }
}
