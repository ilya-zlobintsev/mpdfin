using Serilog;

namespace Mpdfin.Player;

public class Queue
{

    public List<QueueItem> Items { get; private set; }

    public bool Random { get; private set; }

    public int Count => Items.Count;

    public int NextItemId { get; private set; }

    public Queue() => Items = [];

    public Queue(List<QueueItem> items, int nextSongId, bool random)
    {
        Items = items;
        NextItemId = nextSongId;
        Random = random;
    }

    public void SetRandom(bool value)
    {

        if (Random == value)
            return;

        if (value)
            Items = [.. Items.OrderBy(_ => System.Random.Shared.Next())];
        else
        {
            Items = [.. Items.OrderBy(item => item.Position)];
            RecalculatePositions();
        }

        Random = value;
    }

    public int Add(Guid songId) => AddWithPosition(Items.Count, songId);

    public int AddWithPosition(int pos, Guid songId)
    {
        Log.Information($"Adding with pos {pos}");
        var item = new QueueItem { Position = pos, Id = NextItemId, SongId = songId };
        Items.Insert(pos, item);
        RecalculatePositions();
        NextItemId++;

        return item.Id;
    }

    public void AddMany(int? startPos, Guid[] songIds)
    {
        Log.Debug($"Adding {songIds.Length} items");

        var pos = startPos ?? Items.Count;
        foreach (var songId in songIds)
        {
            var item = new QueueItem { Position = pos, Id = NextItemId, SongId = songId };
            Items.Insert(pos, item);
            NextItemId++;
            pos++;
        }

        RecalculatePositions();
    }

    public void Clear() => Items.Clear();

    public void RemoveAt(int pos)
    {
        Items.RemoveAll(item => item.Position == pos);
        RecalculatePositions();
    }

    public void RemoveById(int id)
    {
        Items.RemoveAll(item => item.Id == id);
        RecalculatePositions();
    }

    public QueueItem? GetById(int id) => Items.FirstOrDefault(item => item.Id == id);

    public int? GetPositionById(int id) => GetById(id)?.Position;

    public int? OffsetPosition(int currentPos, int offset)
    {
        var index = GetIndexByPosition(currentPos);
        if (index is not null)
        {
            var nextIndex = index + offset;
            return nextIndex < Count && nextIndex >= 0 ? Items[nextIndex.Value].Position : null;
        }
        else
            return null;
    }

    private int? GetIndexByPosition(int pos) => Items.FindIndex(item => item.Position == pos);

    private void RecalculatePositions()
    {
        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            item.Position = i;
            Items[i] = item;
        }
    }

    public QueueItem? ItemAtPosition(int pos) => Items.FirstOrDefault(item => item.Position == pos);

    public IEnumerable<QueueItem> AsEnumerable() => Items.OrderBy(item => item.Position);
}
