namespace SmartBets.Entities;

public class LeagueTopCard
{
    public long Id { get; set; }
    public long LeagueId { get; set; }
    public int Season { get; set; }
    public int CombinedRank { get; set; }
    public int? YellowRank { get; set; }
    public int? RedRank { get; set; }
    public long ApiPlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string? PlayerPhotoUrl { get; set; }
    public long? TeamId { get; set; }
    public long? TeamApiId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamLogoUrl { get; set; }
    public int? Appearances { get; set; }
    public int? Minutes { get; set; }
    public string? Position { get; set; }
    public decimal? Rating { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public DateTime SyncedAtUtc { get; set; }

    public League League { get; set; } = null!;
}
