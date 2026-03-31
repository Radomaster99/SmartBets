namespace SmartBets.Dtos;

public class SyncErrorDto
{
    public long Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public long? LeagueApiId { get; set; }
    public int? Season { get; set; }
    public string Source { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
}
