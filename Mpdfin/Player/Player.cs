using System.Threading.Channels;
using Jellyfin.Sdk;
using LibVLCSharp.Shared;
using Serilog;

namespace Mpdfin.Player;

public class Player
{
    readonly LibVLC libVLC;
    readonly MediaPlayer MediaPlayer;

    public List<Song> Queue { get; }
    public int PlaylistVersion;
    int? CurrentItem;

    readonly ChannelWriter<Subsystem> EventWriter;

    public Player(ChannelWriter<Subsystem> eventWriter)
    {
        EventWriter = eventWriter;

        libVLC = new();
        MediaPlayer = new(libVLC);

        Queue = new();
        PlaylistVersion = 0;

        MediaPlayer.EndReached += (_, _) => NextSong();
    }

    public void PlayCurrent()
    {
        if (CurrentItem < Queue.Count)
        {
            var song = Queue[CurrentItem.Value];
            Media media = new(libVLC, song.Uri);
            MediaPlayer.Play(media);
        }
        EventWriter.TryWrite(Subsystem.player);
    }

    public void Stop()
    {
        MediaPlayer.Stop();
        CurrentItem = 0;
        EventWriter.TryWrite(Subsystem.player);
    }

    /// <summary>
    /// Adds a song to queue an returns the id
    /// </summary>
    public Guid Add(Uri url, BaseItemDto item)
    {
        Song song = new(url, item);
        Queue.Add(song);
        PlaylistVersion++;
        EventWriter.TryWrite(Subsystem.playlist);
        return song.Id;
    }

    public void SetCurrent(int newPosition)
    {
        CurrentItem = newPosition;
        PlayCurrent();
    }

    public void NextSong()
    {
        if (CurrentItem < Queue.Count - 1)
        {
            Log.Debug("Switching to next item");
            CurrentItem += 1;
            PlayCurrent();
        }
        else
        {
            Log.Debug("End of playlist reached");
            CurrentItem = null;
            EventWriter.TryWrite(Subsystem.playlist);
        }
    }

    public int Volume
    {
        get
        {
            return MediaPlayer.Volume;
        }
        set
        {
            MediaPlayer.Volume = value;
            EventWriter.TryWrite(Subsystem.mixer);
        }
    }

    public VLCState State
    {
        get
        {
            return MediaPlayer.State;
        }
    }

    public void SetPause(bool? pause)
    {
        if (pause is not null)
        {
            MediaPlayer.SetPause(pause.Value);
        }
        else
        {
            MediaPlayer.Pause();
        }
        EventWriter.TryWrite(Subsystem.player);
    }
}
