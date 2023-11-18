namespace Mpdfin.Mpd;

partial class CommandHandler
{
    Response Play(string? posArg)
    {
        if (posArg is not null)
        {
            var pos = int.Parse(posArg);

            if (pos >= Player.Queue.Count)
                throw new FileNotFoundException("Invalid song index");

            Player.SetCurrentPosition(pos);
        }
        else
        {
            Player.SetPause(false);
        }

        return new();
    }

    Response PlayId(int id)
    {
        Player.SetCurrentId(id);
        return new();
    }

    Response Pause(string? state)
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

    Response Stop()
    {
        Player.Stop();
        return new();
    }

    Response GetVol() => new("volume"u8, Player.Volume.ToString());

    Response SetVol(int vol)
    {
        Player.Volume = vol;
        return new();
    }

    Response Volume(int change)
    {
        Player.Volume += change;
        return new();
    }

    Response Seek(int songPos, double time)
    {
        if (Player.CurrentPos != songPos)
        {
            Player.SetCurrentPosition(songPos);
        }
        return SeekCur(time);
    }

    Response SeekId(int id, double time)
    {
        var songPos = Player.Queue.GetPositionById(id)!;
        return Seek(songPos.Value, time);

    }

    Response SeekCur(double time)
    {
        Player.Seek(time);
        return new();
    }

    Response Next()
    {
        Player.PlayNextSong();
        return new();
    }

    Response Previous()
    {
        Player.PreviousSong();
        return new();
    }

    static Response ReplayGainStatus()
    {
        return new("replay_gain_mode"u8, "off");
    }
}
