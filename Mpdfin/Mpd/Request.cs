using System.Text;

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
    public readonly List<string> Args;

    static Command ParseCommand(ReadOnlySpan<char> rawCommand)
    {
        return !int.TryParse(rawCommand, out int _)
            && Enum.TryParse(rawCommand, false, out Command command)
                ? command : throw new Exception($"unknown command {rawCommand}");
    }

    public Request(string raw)
    {
        var i = raw.IndexOf(' ');
        i = i >= 0 ? i : raw.Length;

        Command = ParseCommand(raw.AsSpan(0, i));

        Args = [];
        StringBuilder currentArgBuilder = new(raw.Length - i);

        for (; i < raw.Length; i++)
        {
            var c = raw[i];
            switch (c)
            {
                case '"':
                    if (currentArgBuilder.Length > 0)
                    {
                        throw new Exception($"Unexpected data before quote: {currentArgBuilder}");
                    }

                    var exitLoop = false;
                    for (i++; i < raw.Length && !exitLoop; i++)
                    {
                        var innerC = raw[i];

                        switch (innerC)
                        {
                            case '"':
                                var arg = currentArgBuilder.ToString();
                                Args.Add(arg);
                                currentArgBuilder.Clear();
                                exitLoop = true;
                                break;
                            case '\\':
                                i++;

                                if (i < raw.Length)
                                {
                                    var escapedChar = raw[i];
                                    currentArgBuilder.Append(escapedChar);
                                }
                                else
                                {
                                    throw new Exception("No character after escape symbol");
                                }
                                break;
                            default:
                                currentArgBuilder.Append(innerC);
                                break;
                        }
                    }
                    break;
                case ' ':
                    if (currentArgBuilder.Length > 0)
                    {
                        var arg = currentArgBuilder.ToString();
                        Args.Add(arg);
                        currentArgBuilder.Clear();
                    }
                    break;
                case '\\':
                    i++;

                    if (i < raw.Length)
                    {
                        var escapedChar = raw[i];
                        currentArgBuilder.Append(escapedChar);
                    }
                    else
                    {
                        throw new Exception("No character after escape symbol");
                    }
                    break;
                default:
                    currentArgBuilder.Append(c);
                    break;
            }
        }

        if (currentArgBuilder.Length > 0)
        {
            var arg = currentArgBuilder.ToString();
            Args.Add(arg);
        }
    }

    public override string ToString()
    {
        return $"{Command} {string.Join(" ", Args)}";
    }

    public static Range ParseRange(string input)
    {
        var separator = input.IndexOf(':');

        var result =
            int.TryParse(input.AsSpan(0, separator), out var start) &
            int.TryParse(input.AsSpan(separator + 1), out var end);

        return result ? new(start, end) : throw new FormatException($"Invalid range {input}");
    }
}
