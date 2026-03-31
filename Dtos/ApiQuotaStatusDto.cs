namespace SmartBets.Dtos;

public class ApiQuotaStatusDto
{
    public string Provider { get; set; } = "api-football";
    public string Mode { get; set; } = "Unknown";
    public int? RequestsDailyLimit { get; set; }
    public int? RequestsDailyRemaining { get; set; }
    public int? RequestsMinuteLimit { get; set; }
    public int? RequestsMinuteRemaining { get; set; }
    public DateTime? LastObservedAtUtc { get; set; }
}
