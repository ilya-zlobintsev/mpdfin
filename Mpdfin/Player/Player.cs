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
    List<Song> RandomQueue { get; set; }
    List<Song> ActiveQueue
    {
        get
        {
            if (Random)
            {
                return RandomQueue;
            }
            else
            {
                return Queue;
            }
        }
    }

    int nextSongId;
    public int PlaylistVersion;
    bool _random;
    public bool Random
    {
        get => _random;
        set
        {
            if (value != _random)
            {
                if (value)
                {
                    if (CurrentPos is not null)
                    {
                        var currentItem = Queue[CurrentPos.Value];

                        RandomQueue = new(Queue);
                        RandomQueue.RemoveAt(CurrentPos.Value);
                        RandomQueue = Queue.OrderBy(_ => System.Random.Shared.Next()).ToList();
                        RandomQueue.Insert(0, currentItem);
                    }
                    else
                    {
                        RandomQueue = Queue.OrderBy(item => System.Random.Shared.Next()).ToList();
                    }

                    CurrentPos = 0;
                }
                else
                {
                    if (CurrentPos is not null)
                    {
                        var currentItem = RandomQueue[CurrentPos.Value];
                        CurrentPos = Queue.FindIndex(item => item.Id == currentItem.Id);
                    }
                }
                _random = value;
                RaiseEvent(Subsystem.options);
            }
        }
    }

    int? CurrentPos;
    public int? QueuePos
    {
        get
        {
            if (CurrentPos is not null)
            {
                return NormalizePosition(CurrentPos.Value);
            }
            else
            {
                return CurrentPos;
            }
        }
    }

    public Song? CurrentSong
    {
        get
        {
            if (CurrentPos is not null)
            {
                return ActiveQueue[CurrentPos.Value];
            }
            else
            {
                return null;
            }
        }
    }

    public (int, Song)? QueueNext
    {
        get
        {
            if (CurrentPos is not null && CurrentPos < ActiveQueue.Count - 1)
            {
                var nextPos = CurrentPos.Value + 1;
                var normalizedPos = NormalizePosition(nextPos);
                return (normalizedPos, ActiveQueue[nextPos]);
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
        set
        {
            lock (MediaPlayer)
            {
                MediaPlayer.Volume = value;
            }
        }
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
                RandomQueue = RandomQueue.ConvertAll(song => (song.Id, song.Item.Id)),
                PlaylistVersion = PlaylistVersion,
                NextSongId = nextSongId,
                PlaybackState = PlaybackState,
                Elapsed = Elapsed,
                Random = Random,
            };
        }
    }

    public float? Duration => CurrentPos is not null ? MediaPlayer.Length / 1000 : null;
    public float? Elapsed => Duration is not null ? Math.Abs(Duration.Value * MediaPlayer.Position) : null;

    public event EventHandler<SubsystemEventArgs>? OnSubsystemUpdate;
    public event EventHandler? OnPlaybackStarted;
    public event EventHandler? OnPlaybackStopped;

    public Player()
    {

        libVLC = new();
        libVLC.SetUserAgent("Mpdfin", "Mpdfin client");
        MediaPlayer = new(libVLC);

        Queue = new();
        RandomQueue = new();
        PlaylistVersion = 1;
        nextSongId = 0;
        Volume = 50;

        MediaPlayer.Playing += (e, args) => PlaybackChanged();
        MediaPlayer.Stopped += (e, args) => PlaybackChanged();
        MediaPlayer.Paused += (e, args) => PlaybackChanged();
        MediaPlayer.VolumeChanged += (e, args) => RaiseEvent(Subsystem.mixer);
        MediaPlayer.Muted += (e, args) => RaiseEvent(Subsystem.mixer);
        MediaPlayer.Unmuted += (e, args) => RaiseEvent(Subsystem.mixer);

        MediaPlayer.Playing += (_, args) => SpawnEventHandler(OnPlaybackStarted, args);
        MediaPlayer.Stopped += (_, args) => SpawnEventHandler(OnPlaybackStopped, args);

        MediaPlayer.EncounteredError += (_, _) => Task.Run(NextSong);
        MediaPlayer.EndReached += (_, _) => Task.Run(NextSong);
    }

    void SpawnEventHandler(EventHandler? handler, EventArgs args)
    {
        if (handler is not null)
        {
            Task.Run(() => handler(this, args));
        }
    }

    public void LoadState(PlayerState state, Database db)
    {
        _random = state.Random;
        CurrentPos = state.CurrentPos;
        Volume = state.Volume;
        PlaylistVersion = state.PlaylistVersion;
        nextSongId = state.NextSongId;

        try
        {
            Song ParseStoredItem((int, Guid) storedItem)
            {
                var item = db.GetItem(storedItem.Item2) ?? throw new Exception("Item in state not found in db");
                var url = db.Client.GetAudioStreamUri(item.Id);
                return new Song(url, item, storedItem.Item1);
            }

            Queue = state.Queue.Select(ParseStoredItem).ToList();
            RandomQueue = state.RandomQueue.Select(ParseStoredItem).ToList();

            Log.Information($"Loaded a queue of {Queue.Count} (randomized: {RandomQueue.Count}) items from state");

            PlayCurrent();

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
        if (CurrentPos < ActiveQueue.Count)
        {
            var song = ActiveQueue[CurrentPos.Value];
            Task.Run(() =>
            {
                lock (MediaPlayer)
                {
                    Media media = new(libVLC, song.Uri);
                    MediaPlayer.Play(media);
                }
            });
        }

        RaiseEvent(Subsystem.player);
        RaiseEvent(Subsystem.mixer);
    }

    public void Stop()
    {
        CurrentPos = null;
        Task.Run(() =>
        {
            lock (MediaPlayer)
            {
                MediaPlayer.Stop();
            }
        });
        PlaybackChanged();
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

    public void SetCurrentPosition(int newPosition)
    {
        CurrentPos = DenormalizePosition(newPosition);
        PlayCurrent();
    }

    public void SetCurrentId(int id)
    {
        var index = ActiveQueue.FindIndex(item => item.Id == id);
        if (index == -1)
            throw new FileNotFoundException($"Song with id {id} not found in the database");

        CurrentPos = index;
        PlayCurrent();
    }

    public void NextSong()
    {
        if (CurrentPos is null)
        {
            throw new Exception("Not currently playing");
        }

        if (CurrentPos < ActiveQueue.Count - 1)
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
            Stop();
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
        lock (MediaPlayer)
        {
            MediaPlayer.SeekTo(TimeSpan.FromSeconds(time));
        }
        RaiseEvent(Subsystem.player);
    }

    public VLCState PlaybackState { get; private set; }
    public AudioOutputDescription[] AudioOutputDevices => libVLC.AudioOutputs;

    public void SetPause(bool? pause)
    {
        lock (MediaPlayer)
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

    }

    public void ShuffleQueue(int start, int end)
    {
        int? currentId = CurrentPos is not null ? Queue[CurrentPos.Value].Id : null;

        var span = CollectionsMarshal.AsSpan(Queue)[start..end];
        System.Random.Shared.Shuffle(span);

        if (currentId is not null)
            CurrentPos = Queue.FindIndex(item => item.Id == currentId);

        PlaylistVersion++;
        RaiseEvent(Subsystem.playlist);
    }

    void PlaybackChanged()
    {
        Task.Run(() =>
        {
            PlaybackState = MediaPlayer.State;
        });
        RaiseEvent(Subsystem.player);
        RaiseEvent(Subsystem.mixer);
    }

    /// actual position in random queue -> position in general queue
    int NormalizePosition(int actualPosition)
    {
        if (!Random)
            return actualPosition;

        var song = ActiveQueue[actualPosition];
        return Queue.FindIndex(item => item.Id == song.Id);
    }

    /// position in general queue -> position in random queue
    int DenormalizePosition(int apparentPosition)
    {
        if (!Random)
            return apparentPosition;

        var song = Queue[apparentPosition];
        return ActiveQueue.FindIndex(item => item.Id == song.Id);
    }
}
