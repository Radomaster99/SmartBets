namespace SmartBets.Entities;

public class LeagueRound
{
    public long Id { get; set; }
    public long LeagueId { get; set; }
    public int Season { get; set; }
    public string RoundName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime SyncedAtUtc { get; set; }

    public League League { get; set; } = null!;
}
