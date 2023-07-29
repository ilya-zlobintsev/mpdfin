using Jellyfin.Sdk;
using LibVLCSharp.Shared;
using LibVLCSharp.Shared.Structures;
using Serilog;

namespace Mpdfin.Player;

public class Player
{
    readonly LibVLC libVLC;
    readonly MediaPlayer MediaPlayer;

    public List<Song> Queue { get; }
    int nextSongId;
    public int PlaylistVersion;

    int? _currentPos;
    public int? CurrentPos => _currentPos;
    public Song? CurrentSong
    {
        get
        {
            if (_currentPos is not null)
            {
                return Queue[_currentPos.Value];
            }
            else
            {
                return null;
            }
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

    public float Duration => MediaPlayer.Length / 1000;
    public float Elapsed => Math.Abs(MediaPlayer.Length / 1000 * MediaPlayer.Position);

    public event EventHandler<SubsystemEventArgs>? OnSubsystemUpdate;

    public Player()
    {
        libVLC = new();
        MediaPlayer = new(libVLC);

        Queue = new();
        PlaylistVersion = 1;
        nextSongId = 0;

        MediaPlayer.Playing += (e, args) => RaisePlaybackChanged();
        MediaPlayer.Stopped += (e, args) => RaisePlaybackChanged();
        MediaPlayer.Paused += (e, args) => RaisePlaybackChanged();

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
        if (CurrentPos < Queue.Count)
        {
            var song = Queue[CurrentPos.Value];
            Media media = new(libVLC, song.Uri);
            MediaPlayer.Play(media);
        }
        RaiseEvent(Subsystem.player);
        RaiseEvent(Subsystem.mixer);
    }

    public void Stop()
    {
        MediaPlayer.Stop();
        _currentPos = 0;
        RaiseEvent(Subsystem.player);
    }

    /// <summary>
    /// Adds a song to queue an returns the id
    /// </summary>
    public int Add(Uri url, BaseItemDto item)
    {
        Song song = new(url, item, nextSongId);
        Queue.Add(song);
        PlaylistVersion++;
        nextSongId++;
        RaiseEvent(Subsystem.playlist);
        return song.Id;
    }

    public void SetCurrent(int newPosition)
    {
        _currentPos = newPosition;
        PlayCurrent();
    }

    public void NextSong()
    {
        if (CurrentPos < Queue.Count - 1)
        {
            Log.Debug("Switching to next item");
            _currentPos += 1;
            PlayCurrent();
        }
        else
        {
            Log.Debug("End of playlist reached");
            _currentPos = null;
            RaiseEvent(Subsystem.playlist);
        }
    }

    public VLCState State => MediaPlayer.State;
    public AudioOutputDescription[] AudioOutputDevices => libVLC.AudioOutputs;

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
    }

    void RaisePlaybackChanged()
    {
        RaiseEvent(Subsystem.player);
        RaiseEvent(Subsystem.mixer);
    }
}
