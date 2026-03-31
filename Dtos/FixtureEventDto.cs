namespace SmartBets.Dtos;

public class FixtureEventDto
{
    public int SortOrder { get; set; }
    public int? Elapsed { get; set; }
    public int? Extra { get; set; }
    public long? TeamId { get; set; }
    public long? TeamApiId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamLogoUrl { get; set; }
    public long? PlayerApiId { get; set; }
    public string? PlayerName { get; set; }
    public long? AssistApiId { get; set; }
    public string? AssistName { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? Comments { get; set; }
    public DateTime SyncedAtUtc { get; set; }
}
