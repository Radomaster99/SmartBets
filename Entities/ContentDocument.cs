namespace SmartBets.Entities;

public class ContentDocument
{
    public long Id { get; set; }
    public string ContentKey { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
