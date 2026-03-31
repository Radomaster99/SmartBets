namespace SmartBets.Entities;

public class FixtureLineup
{
    public long Id { get; set; }
    public long FixtureId { get; set; }
    public long? TeamId { get; set; }
    public long ApiTeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamLogoUrl { get; set; }
    public string? Formation { get; set; }
    public long? CoachApiId { get; set; }
    public string? CoachName { get; set; }
    public string? CoachPhotoUrl { get; set; }
    public bool IsStarting { get; set; }
    public int SortOrder { get; set; }
    public long? PlayerApiId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int? PlayerNumber { get; set; }
    public string? PlayerPosition { get; set; }
    public string? PlayerGrid { get; set; }
    public DateTime SyncedAtUtc { get; set; }

    public Fixture Fixture { get; set; } = null!;
}
