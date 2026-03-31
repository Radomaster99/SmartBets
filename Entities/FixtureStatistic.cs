namespace SmartBets.Entities;

public class FixtureStatistic
{
    public long Id { get; set; }
    public long FixtureId { get; set; }
    public long? TeamId { get; set; }
    public long ApiTeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamLogoUrl { get; set; }
    public int SortOrder { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Value { get; set; }
    public DateTime SyncedAtUtc { get; set; }

    public Fixture Fixture { get; set; } = null!;
}
