namespace Mpdfin.Mpd;

partial class CommandHandler
{
    public Response Play(int pos)
    {
        if (pos >= Player.Queue.Count)
            throw new FileNotFoundException("Invalid song index");

        Player.SetCurrent(pos);

        return new();
    }

    public Response PlayId(string rawId)
    {
        var id = int.Parse(rawId);
        var index = Player.Queue.FindIndex(song => song.Id == id);

        if (index == -1)
            throw new FileNotFoundException($"Song with id {id} not found in the database");

        Player.SetCurrent(index);

        return new();
    }

    public Response Pause()
    {
        return Pause(null);
    }

    public Response Pause(string? state)
    {
        bool? value = state switch
        {
            "0" => false,
            "1" => true,
            _ => null,
        };
        Player.SetPause(value);
        return new();
    }

    public Response Stop()
    {
        Player.Stop();
        return new();
    }

    public Response GetVol() => new("volume"u8, Player.Volume.ToString());

    public Response SetVol(int vol)
    {
        Player.Volume = vol;
        return new();
    }

    public Response Volume(int change)
    {
        Player.Volume += change;
        return new();
    }

    public Response Seek(int songPos, double time)
    {
        if (Player.CurrentPos != songPos)
        {
            Player.SetCurrent(songPos);
        }
        return SeekCur(time);
    }

    public Response SeekId(int id, double time)
    {
        var songPos = Player.Queue.FindIndex(song => song.Id == id);
        return Seek(songPos, time);
    }

    public Response SeekCur(double time)
    {
        Player.Seek(time);
        return new();
    }

    public Response Next()
    {
        Player.NextSong();
        return new();
    }

    public Response Previous()
    {
        Player.PreviousSong();
        return new();
    }

    public static Response ReplayGainStatus()
    {
        return new("replay_gain_mode"u8, "off");
    }
}
