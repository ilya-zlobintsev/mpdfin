namespace Mpdfin;

enum Command
{
    ping,

    status,
    currentsong,
    playid,
    pause,
    getvol,
    setvol,
    addid,

    command_list_begin,
    command_list_ok_begin,
    command_list_end,
}

readonly record struct Request(Command Command, List<string> Args);
