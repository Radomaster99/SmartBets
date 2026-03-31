namespace SmartBets.Entities;

public class FixturePlayerStatistic
{
    public long Id { get; set; }
    public long FixtureId { get; set; }
    public long? TeamId { get; set; }
    public long ApiTeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamLogoUrl { get; set; }
    public int SortOrder { get; set; }
    public long PlayerApiId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string? PlayerPhotoUrl { get; set; }
    public int? Minutes { get; set; }
    public int? Number { get; set; }
    public string? Position { get; set; }
    public decimal? Rating { get; set; }
    public bool IsCaptain { get; set; }
    public bool IsSubstitute { get; set; }
    public int? Offsides { get; set; }
    public int? ShotsTotal { get; set; }
    public int? ShotsOn { get; set; }
    public int? GoalsTotal { get; set; }
    public int? GoalsConceded { get; set; }
    public int? GoalsAssists { get; set; }
    public int? GoalsSaves { get; set; }
    public int? PassesTotal { get; set; }
    public int? PassesKey { get; set; }
    public string? PassesAccuracy { get; set; }
    public int? TacklesTotal { get; set; }
    public int? TacklesBlocks { get; set; }
    public int? TacklesInterceptions { get; set; }
    public int? DuelsTotal { get; set; }
    public int? DuelsWon { get; set; }
    public int? DribblesAttempts { get; set; }
    public int? DribblesSuccess { get; set; }
    public int? DribblesPast { get; set; }
    public int? FoulsDrawn { get; set; }
    public int? FoulsCommitted { get; set; }
    public int? CardsYellow { get; set; }
    public int? CardsRed { get; set; }
    public int? PenaltyWon { get; set; }
    public int? PenaltyCommitted { get; set; }
    public int? PenaltyScored { get; set; }
    public int? PenaltyMissed { get; set; }
    public int? PenaltySaved { get; set; }
    public DateTime SyncedAtUtc { get; set; }

    public Fixture Fixture { get; set; } = null!;
}
