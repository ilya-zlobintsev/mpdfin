using LibVLCSharp.Shared;
using LibVLCSharp.Shared.Structures;
using Mpdfin.DB;
using Mpdfin.MediaKeys;
using Serilog;

namespace Mpdfin.Player;

public class Player
{
    readonly LibVLC libVLC;
    readonly MediaPlayer MediaPlayer;
    readonly Database database;

    public Queue Queue { get; set; }

    public int PlaylistVersion;

    public int? CurrentPos { get; private set; }
    public int? NextPos => CurrentPos is not null ? Queue.OffsetPosition(CurrentPos.Value, 1) : null;

    public QueueItem? CurrentSong => CurrentPos is not null ? Queue.ItemAtPosition(CurrentPos.Value) : null;
    public QueueItem? NextSong => NextPos is not null ? Queue.ItemAtPosition(NextPos.Value) : null;

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

    public PlayerState State => new()
    {
        Volume = Volume,
        CurrentPos = CurrentPos,
        QueueItems = Queue.Items,
        NextSongId = Queue.NextItemId,
        Random = Queue.Random,
        PlaylistVersion = PlaylistVersion,
        PlaybackState = PlaybackState,
        Elapsed = Elapsed,
    };

    public float? Duration => CurrentPos is not null ? MediaPlayer.Length / 1000 : null;
    public float? Elapsed => Duration is not null ? Math.Abs(Duration.Value * MediaPlayer.Position) : null;

    public event EventHandler<Subsystem>? OnSubsystemUpdate;
    public event EventHandler? OnPlaybackStarted;
    public event EventHandler? OnPlaybackStopped;

    readonly MediaKeysService mediaKeysService;
    readonly CallbackMediaControlEvent handleMediaControlEvent;

    public Player(Database db)
    {
        database = db;

        libVLC = new();
        libVLC.SetUserAgent("Mpdfin", "Mpdfin client");
        MediaPlayer = new(libVLC);

        Queue = new();
        PlaylistVersion = 1;
        Volume = 50;
        mediaKeysService = MediaKeysService.New("mpdfin");

        MediaPlayer.Playing += (e, args) => PlaybackChanged();
        MediaPlayer.Stopped += (e, args) => PlaybackChanged();
        MediaPlayer.Paused += (e, args) => PlaybackChanged();
        MediaPlayer.VolumeChanged += (e, args) => RaiseEvent(Subsystem.mixer);
        MediaPlayer.Muted += (e, args) => RaiseEvent(Subsystem.mixer);
        MediaPlayer.Unmuted += (e, args) => RaiseEvent(Subsystem.mixer);

        MediaPlayer.Playing += (_, args) => SpawnEventHandler(OnPlaybackStarted, args);
        MediaPlayer.Stopped += (_, args) => SpawnEventHandler(OnPlaybackStopped, args);

        MediaPlayer.EncounteredError += (_, _) => Task.Run(PlayNextSong);
        MediaPlayer.EndReached += (_, _) => Task.Run(PlayNextSong);

        handleMediaControlEvent = new CallbackMediaControlEvent((mediaEvent) =>
        {
            switch (mediaEvent)
            {
                case FFIMediaControlEvent.Play:
                    PlayCurrent();
                    break;
                case FFIMediaControlEvent.Pause:
                    SetPause(true);
                    break;
                case FFIMediaControlEvent.Toggle:
                    SetPause();
                    break;
                case FFIMediaControlEvent.Next:
                    PlayNextSong();
                    break;
                case FFIMediaControlEvent.Previous:
                    PreviousSong();
                    break;
                case FFIMediaControlEvent.Stop:
                    Stop();
                    break;
            }
        });
        mediaKeysService.Attach(handleMediaControlEvent);

        OnSubsystemUpdate += (_, _) => UpdateMetadata();
    }

    void SpawnEventHandler(EventHandler? handler, EventArgs args)
    {
        if (handler is not null)
        {
            Task.Run(() => handler(this, args));
        }
    }

    public void SetRandom(bool value)
    {
        Queue.SetRandom(value);
        RaiseEvent(Subsystem.options);
    }

    public void LoadState(PlayerState state, Database db)
    {
        var queueItems = state.QueueItems;
        if (queueItems != null)
        {
            Queue = new(
                queueItems as List<QueueItem> ?? [.. queueItems],
                state.NextSongId,
                state.Random);
            Log.Information($"Loaded a queue of {Queue.Count} items from state");
        }

        if (state.CurrentPos < Queue.Count)
            CurrentPos = state.CurrentPos;

        Volume = state.Volume;
        PlaylistVersion = state.PlaylistVersion;

        try
        {
            if (CurrentPos is not null)
            {
                var item = Queue.ItemAtPosition(CurrentPos.Value);
                if (item is not null)
                {
                    Media media = new(libVLC, db.Client.GetAudioStreamUri(item.SongId));
                    media.AddOption(":start-paused");
                    MediaPlayer.Play(media);
                }
            }

            Log.Debug($"Loading playbackstate {Enum.GetName(state.PlaybackState)}");
            switch (state.PlaybackState)
            {
                case VLCState.Playing:
                    PlayCurrent();
                    break;
                case VLCState.Paused:
                    SetPause(true);
                    break;
                default:
                    Stop();
                    break;
            }
            Log.Debug("State restored");

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
        catch
        {
            Log.Error("Could not restore state");
            throw;
        }
    }

    void RaiseEvent(Subsystem subsystem)
    {
        Log.Debug($"Raising event `{subsystem}`");
        if (OnSubsystemUpdate is not null)
        {
            OnSubsystemUpdate(this, subsystem);
        }
        else
        {
            Log.Debug("No event subscribers");
        }
    }

    public void PlayCurrent()
    {
        Log.Debug("Playing");
        var item = CurrentSong;
        if (item is not null)
        {
            Task.Run(() =>
            {
                lock (MediaPlayer)
                {
                    Media media = new(libVLC, database.Client.GetAudioStreamUri(item.SongId));
                    var currentMedia = MediaPlayer.Media;
                    if (currentMedia is null || currentMedia.Mrl != media.Mrl)
                        MediaPlayer.Play(media);
                    else
                        MediaPlayer.Play();
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
    public int Add(Guid songId, int? pos = null)
    {
        var id = AddItem(songId, pos);
        RaiseEvent(Subsystem.playlist);
        return id;
    }

    public void DeleteId(int id)
    {
        Queue.RemoveById(id);
        PlaylistVersion++;
        RaiseEvent(Subsystem.playlist);
    }

    public void DeletePos(int pos)
    {
        Queue.RemoveAt(pos);

        if (CurrentPos == pos)
        {
            PlayCurrent();
        }

        PlaylistVersion++;
        RaiseEvent(Subsystem.playlist);
    }

    public void DeleteRange(Range queueSlice)
    {
        var start = queueSlice.Start.Value;
        var end = queueSlice.End.Value;

        for (var i = start; i < end; i++)
        {
            Log.Debug($"Deleting item {i}");
            DeletePos(i);
        }
        RaiseEvent(Subsystem.playlist);
    }

    public void AddMany(Guid[] songIds, int? pos = null)
    {
        Queue.AddMany(pos, songIds);
        PlaylistVersion++;
        RaiseEvent(Subsystem.playlist);
    }

    int AddItem(Guid songId, int? pos = null)
    {
        int itemId = pos is not null
            ? Queue.AddWithPosition(pos.Value, songId)
            : Queue.Add(songId);
        PlaylistVersion++;
        return itemId;
    }

    public void SetCurrentPosition(int newPosition)
    {
        CurrentPos = newPosition;
        PlayCurrent();
    }

    public void SetCurrentId(int id)
    {
        CurrentPos = Queue.GetPositionById(id) ?? throw new FileNotFoundException($"Song with id {id} not found in the database");
        PlayCurrent();
    }

    public void PlayNextSong() => OffsetPosition(1);

    public void PreviousSong() => OffsetPosition(-1);

    private void OffsetPosition(int offset)
    {
        if (CurrentPos is null)
        {
            throw new Exception("Not currently playing");
        }

        var newPosition = Queue.OffsetPosition(CurrentPos.Value, offset);
        if (newPosition is not null)
        {
            Log.Debug($"Switching to item by offset {offset} at position {newPosition}");
            CurrentPos = newPosition;
            PlayCurrent();
        }
        else
        {
            Log.Debug("End of playlist reached");
            Stop();
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

    public void SetPause(bool? pause = null)
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

    public void ShuffleQueue(Range queueSlice)
    {
        Queue.Shuffle(queueSlice);

        PlaylistVersion++;
        RaiseEvent(Subsystem.playlist);
    }

    void PlaybackChanged()
    {
        PlaybackState = MediaPlayer.State;
        RaiseEvent(Subsystem.player);
        RaiseEvent(Subsystem.mixer);
    }

    void UpdateMetadata()
    {
        try
        {
            var mediaPlayback = PlaybackState switch
            {
                VLCState.Playing => FFIMediaPlayback.Playing,
                VLCState.Paused => FFIMediaPlayback.Paused,
                _ => FFIMediaPlayback.Stopped,
            };
            mediaKeysService.SetPlayback(mediaPlayback);

            var currentSong = CurrentSong;

            FFIMediaMetadata metadata;

            if (currentSong is not null)
            {
                var song = database.GetItem(currentSong.SongId) ?? throw new Exception("Could not find id in the database");

                metadata = new()
                {
                    title = song.Name,
                    album = song.Album,
                    artist = string.Join(", ", song.Artists),
                };
            }
            else
            {
                metadata = new();
            }
            mediaKeysService.SetMetadata(metadata);
        }
        catch (Exception ex)
        {
            Log.Error($"Could not update metadata: {ex}");
        }
    }
}
