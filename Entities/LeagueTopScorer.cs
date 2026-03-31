namespace SmartBets.Entities;

public class LeagueTopScorer
{
    public long Id { get; set; }
    public long LeagueId { get; set; }
    public int Season { get; set; }
    public int Rank { get; set; }
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
    public int? Goals { get; set; }
    public int? Assists { get; set; }
    public int? ShotsTotal { get; set; }
    public int? ShotsOn { get; set; }
    public int? PenaltiesScored { get; set; }
    public DateTime SyncedAtUtc { get; set; }

    public League League { get; set; } = null!;
}
