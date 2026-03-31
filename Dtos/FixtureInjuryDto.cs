namespace SmartBets.Dtos;

public class FixtureInjuryDto
{
    public long? TeamId { get; set; }
    public long? TeamApiId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamLogoUrl { get; set; }
    public long? PlayerApiId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string? PlayerPhotoUrl { get; set; }
    public string? Type { get; set; }
    public string? Reason { get; set; }
    public DateTime SyncedAtUtc { get; set; }
}
