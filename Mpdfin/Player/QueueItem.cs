namespace Mpdfin.Player;

public record QueueItem
{
    public required int Position { get; set; }
    public required int Id { get; init; }
    public required Guid SongId { get; init; }
}
