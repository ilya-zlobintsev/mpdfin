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

    public event EventHandler<SubsystemEventArgs>? OnSubsystemUpdate;

    public Player()
    {
        libVLC = new();
        MediaPlayer = new(libVLC);

        Queue = new();
        PlaylistVersion = 0;

        MediaPlayer.EndReached += (_, _) => NextSong();
    }

    void RaiseEvent(Subsystem subsystem)
    {
        Log.Debug($"Raising event `{subsystem}`");
        if (OnSubsystemUpdate is not null)
        {
            SubsystemEventArgs args = new(subsystem);
            OnSubsystemUpdate(this, args);
        }
        else
        {
            Log.Debug("No event subscribers");
        }
    }

    public void PlayCurrent()
    {
        if (CurrentItem < Queue.Count)
        {
            var song = Queue[CurrentItem.Value];
            Media media = new(libVLC, song.Uri);
            MediaPlayer.Play(media);
        }
        RaiseEvent(Subsystem.player);
    }

    public void Stop()
    {
        MediaPlayer.Stop();
        CurrentItem = 0;
        RaiseEvent(Subsystem.player);
    }

    /// <summary>
    /// Adds a song to queue an returns the id
    /// </summary>
    public Guid Add(Uri url, BaseItemDto item)
    {
        Song song = new(url, item);
        Queue.Add(song);
        PlaylistVersion++;
        RaiseEvent(Subsystem.playlist);
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
            RaiseEvent(Subsystem.playlist);
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
            RaiseEvent(Subsystem.mixer);
        }
    }

    public VLCState State => MediaPlayer.State;

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
        RaiseEvent(Subsystem.player);
    }
}
