namespace SmartBets.Entities;

public class TheOddsLeagueMapping
{
    public long Id { get; set; }
    public long ApiFootballLeagueId { get; set; }
    public string LeagueName { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string? TheOddsSportKey { get; set; }
    public string ResolutionSource { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public bool IsVerified { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? LastResolvedAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
}
