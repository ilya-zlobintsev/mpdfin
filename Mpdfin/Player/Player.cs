using System.Runtime.InteropServices;
using Jellyfin.Sdk;
using LibVLCSharp.Shared;
using LibVLCSharp.Shared.Structures;
using Mpdfin.DB;
using Serilog;

namespace Mpdfin.Player;

public class Player
{
    readonly LibVLC libVLC;
    readonly MediaPlayer MediaPlayer;

    public List<Song> Queue { get; private set; }
    int nextSongId;
    public int PlaylistVersion;

    public int? CurrentPos { get; private set; }
    public Song? CurrentSong
    {
        get
        {
            if (CurrentPos is not null)
            {
                return Queue[CurrentPos.Value];
            }
            else
            {
                return null;
            }
        }
    }

    public int Volume
    {
        get => MediaPlayer.Volume;
        set => MediaPlayer.Volume = value;
    }

    public PlayerState State
    {
        get
        {
            return new PlayerState
            {
                Volume = Volume,
                CurrentPos = CurrentPos,
                Queue = Queue.ConvertAll(song => (song.Id, song.Item.Id)),
                PlaylistVersion = PlaylistVersion,
                NextSongId = nextSongId,
                PlaybackState = PlaybackState,
                Elapsed = Elapsed,
            };
        }
    }

    public float? Duration => CurrentPos is not null ? MediaPlayer.Length / 1000 : null;
    public float? Elapsed => Duration is not null ? Math.Abs(Duration.Value * MediaPlayer.Position) : null;

    public event EventHandler<SubsystemEventArgs>? OnSubsystemUpdate;

    public Player()
    {
        libVLC = new();
        MediaPlayer = new(libVLC);

        Queue = new();
        PlaylistVersion = 1;
        nextSongId = 0;
        Volume = 50;

        MediaPlayer.Playing += (e, args) => RaisePlaybackChanged();
        MediaPlayer.Stopped += (e, args) => RaisePlaybackChanged();
        MediaPlayer.Paused += (e, args) => RaisePlaybackChanged();
        MediaPlayer.VolumeChanged += (e, args) => RaiseEvent(Subsystem.mixer);
        MediaPlayer.Muted += (e, args) => RaiseEvent(Subsystem.mixer);
        MediaPlayer.Unmuted += (e, args) => RaiseEvent(Subsystem.mixer);

        MediaPlayer.EndReached += (_, _) => Task.Run(NextSong);
    }

    public Player(PlayerState state, Database db) : this()
    {
        CurrentPos = state.CurrentPos;
        Volume = state.Volume;

        try
        {
            foreach (var (songId, itemId) in state.Queue)
            {
                var item = db.GetItem(itemId) ?? throw new Exception("Item in queue not found in db");
                var url = db.GetAudioStreamUri(itemId);
                Add(url, item);
            }
            Log.Debug($"Loaded a queue of {state.Queue.Count} items from state");

            PlayCurrent();
            MediaPlayer.Playing += (_, _) => Log.Debug("Media playing");

            switch (state.PlaybackState)
            {
                case VLCState.Playing:
                    break;
                case VLCState.Paused:
                    SetPause(true);
                    break;
                default:
                    Stop();
                    break;
            }

            if (state.Elapsed is not null)
            {
                void SeekOnce(object? sender, EventArgs? args)
                {
                    Task.Run(() => Seek(state.Elapsed!.Value));
                    MediaPlayer.Playing -= SeekOnce;
                }
                MediaPlayer.Playing += SeekOnce;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Could not restore state: {ex}");
        }
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
        CurrentPos = null;
        Task.Run(MediaPlayer.Stop);
        RaisePlaybackChanged();
    }

    public void ClearQueue()
    {
        Stop();
        Queue.Clear();
        RaiseEvent(Subsystem.playlist);
    }

    /// <summary>
    /// Adds a song to queue an returns the id
    /// </summary>
    public int Add(Uri url, BaseItemDto item, int? pos = null)
    {

        var id = AddItem(url, item, pos);
        RaiseEvent(Subsystem.playlist);
        return id;
    }

    public void AddMany((BaseItemDto, Uri)[] items, int? pos = null)
    {
        foreach (var (item, url) in items)
        {
            AddItem(url, item, pos);
            if (pos is not null)
            {
                pos++;
            }
        }
        RaiseEvent(Subsystem.playlist);
    }

    int AddItem(Uri url, BaseItemDto item, int? pos = null)
    {
        Song song = new(url, item, nextSongId);

        if (pos is not null)
        {
            Queue.Insert(pos.Value, song);
            if (pos >= CurrentPos)
            {
                CurrentPos++;
            }
        }
        else
        {
            Queue.Add(song);
        }

        PlaylistVersion++;
        nextSongId++;
        return song.Id;
    }

    public void SetCurrent(int newPosition)
    {
        CurrentPos = newPosition;
        PlayCurrent();
    }

    public void NextSong()
    {
        if (CurrentPos is null)
        {
            throw new Exception("Not currently playing");
        }

        if (CurrentPos < Queue.Count - 1)
        {
            Log.Debug("Switching to next item");
            CurrentPos += 1;
            PlayCurrent();
        }
        else
        {
            Log.Debug("End of playlist reached");
            Stop();
        }
    }

    public void PreviousSong()
    {
        if (CurrentPos is null)
        {
            throw new Exception("Not currently playing");
        }

        if (CurrentPos > 0)
        {
            Log.Debug("Switching to previous item");
            CurrentPos -= 1;
            PlayCurrent();
        }
    }

    public void Seek(double time)
    {
        Log.Debug($"Seeking to {time}");
        MediaPlayer.SeekTo(TimeSpan.FromSeconds(time));
        RaiseEvent(Subsystem.player);
    }

    public VLCState PlaybackState => MediaPlayer.State;
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

    public void ShuffleQueue(int start, int end)
    {
        int? currentId = CurrentPos is not null ? Queue[CurrentPos.Value].Id : null;

        var span = CollectionsMarshal.AsSpan(Queue)[start..end];
        Random.Shared.Shuffle(span);

        if (currentId is not null)
            CurrentPos = Queue.FindIndex(item => item.Id == currentId);

        PlaylistVersion++;
        RaiseEvent(Subsystem.playlist);
    }

    void RaisePlaybackChanged()
    {
        RaiseEvent(Subsystem.player);
        RaiseEvent(Subsystem.mixer);
    }
}
