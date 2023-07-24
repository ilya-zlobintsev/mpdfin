namespace Mpdfin.Mpd;

partial class CommandHandler
{
    Response Play(int pos)
    {
        if (pos >= Player.Queue.Count)
            throw new FileNotFoundException("Invalid song index");

        Player.SetCurrent(pos);

        return new();
    }

    Response PlayId(string id)
    {
        var guid = Guid.Parse(id);
        var index = Player.Queue.FindIndex(song => song.Id == guid);

        if (index == -1)
            throw new FileNotFoundException($"Song with id {id} not found in the database");

        Player.SetCurrent(index);

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

    Response GetVol()
    {
        return new("volume"u8, Player.Volume.ToString());
    }

    Response SetVol(int vol)
    {
        Player.Volume = vol;
        return new();
    }
}