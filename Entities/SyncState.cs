namespace SmartBets.Entities;

public class SyncState
{
    public long Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public long? LeagueApiId { get; set; }
    public int? Season { get; set; }
    public DateTime LastSyncedAt { get; set; }
}