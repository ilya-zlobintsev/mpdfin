using Jellyfin.Sdk;
using LibVLCSharp.Shared;
using Serilog;

namespace Mpdfin;

class Player
{
    public readonly LibVLC libVLC;
    public readonly MediaPlayer MediaPlayer;

    public List<Song> Queue { get; }
    public int PlaylistVersion;
    int? CurrentItem;

    public Player()
    {
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
    }

    public void Stop()
    {
        MediaPlayer.Stop();
        CurrentItem = 0;
    }

    /// <summary>
    /// Adds a song to queue an returns the id
    /// </summary>
    public Guid Add(Uri url, BaseItemDto item)
    {
        Song song = new(url, item);
        Queue.Add(song);
        PlaylistVersion++;
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
        }
    }
}
