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
    clear,
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
    replay_gain_status,

    command_list_begin,
    command_list_ok_begin,
    command_list_end,
}

public readonly record struct Request
{
    public readonly string Command;
    public readonly List<string> Args;

    public Request(string raw)
    {
        var chars = raw.ToCharArray();

        StringBuilder commandBuilder = new();
        int i;

        for (i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c == ' ')
            {
                Command = commandBuilder.ToString();
                break;
            }
            else
            {
                commandBuilder.Append(c);
            }
        }

        Command ??= commandBuilder.ToString();
        Command = Command.ToLower().Replace("_", "");

        Args = new();
        StringBuilder currentArgBuilder = new();

        for (; i < chars.Length; i++)
        {
            var c = chars[i];
            switch (c)
            {
                case '"':
                    if (currentArgBuilder.Length > 0)
                    {
                        throw new Exception($"Unexpected data before quote: {currentArgBuilder}");
                    }

                    var exitLoop = false;
                    for (i++; i < chars.Length && !exitLoop; i++)
                    {
                        var innerC = chars[i];

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

                                if (i < chars.Length)
                                {
                                    var escapedChar = chars[i];
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

                    if (i < chars.Length)
                    {
                        var escapedChar = chars[i];
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
}
