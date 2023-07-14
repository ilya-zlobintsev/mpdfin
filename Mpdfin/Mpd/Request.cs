using System.Globalization;

namespace Mpdfin;

public enum Command
{
    ping,

    status,
    currentsong,
    playid,
    pause,
    getvol,
    setvol,
    addid,
    playlistinfo,
    plchanges,

    command_list_begin,
    command_list_ok_begin,
    command_list_end,
}

public readonly record struct Request
{
    public readonly Command Command;
    public readonly List<string> Args;

    public Request(string raw)
    {
        var split = raw.Split();

        if (split.Length != 0)
        {
            var rawCommand = split[0];

            // Make sure the command isn't a numeric representation of the enum
            if (!int.TryParse(rawCommand, out int _) && Enum.TryParse(rawCommand, false, out Command command))
            {
                Command = command;
                Args = new();

                for (int i = 1; i < split.Length; i++)
                {
                    Args.Add(split[i]);
                }
            }
            else
            {
                throw new Exception($"unknown command {rawCommand}");
            }
        }
        else
        {
            throw new Exception("Empty line provided");
        }
    }
}
