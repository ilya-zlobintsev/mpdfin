using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Serilog;

namespace Mpdfin.Player;

public class Queue
{
    private List<QueueItem> _items;

    public IReadOnlyList<QueueItem> Items => _items;

    public bool Random { get; private set; }

    public int Count => _items.Count;

    public int NextItemId { get; private set; }

    public Queue() => _items = [];

    public Queue(List<QueueItem> items, int nextSongId, bool random)
    {
        _items = items;
        NextItemId = nextSongId;
        Random = random;
    }

    public void SetRandom(bool value)
    {
        if (Random == value)
            return;

        if (value)
        {
            _items = [.. _items.OrderBy(_ => System.Random.Shared.Next())];
        }
        else
        {
            _items = [.. _items.OrderBy(item => item.Position)];
            RecalculatePositions();
        }

        Random = value;
    }

    public int Add(Guid songId) => AddWithPosition(_items.Count, songId);

    public int AddWithPosition(int pos, Guid songId)
    {
        Log.Information($"Adding with pos {pos}");
        var item = new QueueItem { Position = pos, Id = NextItemId, SongId = songId };
        _items.Insert(pos, item);
        RecalculatePositions();
        NextItemId++;

        return item.Id;
    }

    public void AddMany(int? startPos, Guid[] songIds)
    {
        Log.Debug($"Adding {songIds.Length} items");

        var pos = startPos ?? _items.Count;
        var index = pos;

        var items = songIds.Select(songId => new QueueItem
        {
            Position = pos++,
            Id = NextItemId++,
            SongId = songId
        });

        _items.InsertRange(index, items);

        RecalculatePositions();
    }

    public void Clear() => _items.Clear();

    public void RemoveAt(int pos)
    {
        _items.RemoveAll(item => item.Position == pos);
        RecalculatePositions();
    }

    public void RemoveById(int id)
    {
        _items.RemoveAll(item => item.Id == id);
        RecalculatePositions();
    }

    public QueueItem? GetById(int id) => _items.Find(item => item.Id == id);

    public int? GetPositionById(int id) => GetById(id)?.Position;

    public int? OffsetPosition(int currentPos, int offset)
    {
        var index = GetIndexByPosition(currentPos);
        if (index is not null)
        {
            var nextIndex = index + offset;
            return nextIndex < Count && nextIndex >= 0 ? _items[nextIndex.Value].Position : null;
        }
        else
        {
            return null;
        }
    }

    private int? GetIndexByPosition(int pos) => _items.FindIndex(item => item.Position == pos);

    private void RecalculatePositions()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            item.Position = i;
            _items[i] = item;
        }
    }

    public QueueItem? ItemAtPosition(int pos) => _items.Find(item => item.Position == pos);

    public IOrderedEnumerable<QueueItem> AsEnumerable() => _items.OrderBy(item => item.Position);

    public void Shuffle(Range queueSlice)
    {
        var items = CollectionsMarshal.AsSpan(_items)[queueSlice];
        RandomNumberGenerator.Shuffle(items);

        RecalculatePositions();
    }
}
