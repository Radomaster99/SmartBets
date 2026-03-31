namespace SmartBets.Entities;

public class FixtureInjury
{
    public long Id { get; set; }
    public long FixtureId { get; set; }
    public long? TeamId { get; set; }
    public long? ApiTeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamLogoUrl { get; set; }
    public long? PlayerApiId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string? PlayerPhotoUrl { get; set; }
    public string? Type { get; set; }
    public string? Reason { get; set; }
    public DateTime SyncedAtUtc { get; set; }

    public Fixture Fixture { get; set; } = null!;
}
