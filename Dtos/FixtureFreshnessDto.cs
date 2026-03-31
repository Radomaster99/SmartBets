namespace SmartBets.Dtos;

public class FixtureFreshnessDto
{
    public DateTime? LastLiveStatusSyncedAtUtc { get; set; }
    public DateTime? LastEventSyncedAtUtc { get; set; }
    public DateTime? LastStatisticsSyncedAtUtc { get; set; }
    public DateTime? LastLineupsSyncedAtUtc { get; set; }
    public DateTime? LastPlayerStatisticsSyncedAtUtc { get; set; }
    public DateTime? LastPredictionSyncedAtUtc { get; set; }
    public DateTime? LastInjuriesSyncedAtUtc { get; set; }
}
