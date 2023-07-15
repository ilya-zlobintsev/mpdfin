namespace Mpdfin.Mpd;

enum Ack
{
    NOT_LIST = 1,
    ARG = 2,
    PASSWORD = 3,
    PERMISSION = 4,
    UNKNOWN = 5,

    NO_EXIST = 50,
    PLAYLIST_MAX = 51,
    SYSTEM = 52,
    PLAYLIST_LOAD = 53,
    UPDATE_ALREADY = 54,
    PLAYER_SYNC = 55,
    EXIST = 56,
}