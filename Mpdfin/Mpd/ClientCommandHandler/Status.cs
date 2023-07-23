using LibVLCSharp.Shared;

namespace Mpdfin.Mpd;

partial class ClientCommandHandler
{
    Response Status()
    {
        Response response = new();

        response.Add("volume"u8, Player.Volume.ToU8String());
        response.Add("state"u8, Player.State switch
        {
            VLCState.Playing => "play"u8,
            VLCState.Paused => "pause"u8,
            _ => "stop"u8,
        });
        response.Add("playlist"u8, Player.PlaylistVersion.ToU8String());
        response.Add("playlistlength"u8, Player.Queue.Count.ToU8String());

        return response;
    }

    static Response CurrentSong()
    {
        return new();
    }
}