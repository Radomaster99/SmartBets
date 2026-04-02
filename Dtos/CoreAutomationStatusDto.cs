namespace SmartBets.Dtos;

public class CoreAutomationStatusDto
{
    public DateTime? CatalogLastRefreshedAtUtc { get; set; }
    public int CurrentLeagueSeasonCount { get; set; }
    public int DailyBudget { get; set; }
    public int UsedToday { get; set; }
    public int RemainingToday { get; set; }
    public IReadOnlyList<CoreAutomationJobStatusDto> Jobs { get; set; } = Array.Empty<CoreAutomationJobStatusDto>();
}

public class CoreAutomationJobStatusDto
{
    public string Job { get; set; } = string.Empty;
    public int DailyBudget { get; set; }
    public int UsedToday { get; set; }
    public int RemainingToday { get; set; }
    public DateTime? LastStartedAtUtc { get; set; }
    public DateTime? LastCompletedAtUtc { get; set; }
    public DateTime? LastSkippedAtUtc { get; set; }
    public string? LastStatus { get; set; }
    public string? LastReason { get; set; }
    public int? LastDesiredRequests { get; set; }
    public int? LastActualRequests { get; set; }
    public int? LastProcessedItems { get; set; }
}
