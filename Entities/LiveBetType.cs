namespace SmartBets.Entities;

public class LiveBetType
{
    public long Id { get; set; }
    public long ApiBetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime SyncedAtUtc { get; set; }

    public ICollection<LiveOdd> LiveOdds { get; set; } = new List<LiveOdd>();
}
