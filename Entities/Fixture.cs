namespace SmartBets.Entities;

public class Fixture
{
    public long Id { get; set; }
    public long ApiFixtureId { get; set; }
    public long LeagueId { get; set; }
    public int Season { get; set; }
    public DateTime KickoffAt { get; set; }
    public string? Status { get; set; }
    public string? StatusLong { get; set; }
    public int? Elapsed { get; set; }
    public int? StatusExtra { get; set; }
    public string? Referee { get; set; }
    public string? Timezone { get; set; }
    public string? VenueName { get; set; }
    public string? VenueCity { get; set; }
    public string? Round { get; set; }
    public long HomeTeamId { get; set; }
    public long AwayTeamId { get; set; }
    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }
    public DateTime? LastLiveStatusSyncedAtUtc { get; set; }
    public DateTime? LastEventSyncedAtUtc { get; set; }
    public DateTime? LastStatisticsSyncedAtUtc { get; set; }
    public DateTime? LastLineupsSyncedAtUtc { get; set; }
    public DateTime? LastPlayerStatisticsSyncedAtUtc { get; set; }
    public DateTime? LastPredictionSyncedAtUtc { get; set; }
    public DateTime? LastInjuriesSyncedAtUtc { get; set; }
    public int PostFinishMatchCenterSyncCount { get; set; }

    public League League { get; set; } = null!;
    public Team HomeTeam { get; set; } = null!;
    public Team AwayTeam { get; set; } = null!;
    public ICollection<PreMatchOdd> PreMatchOdds { get; set; } = new List<PreMatchOdd>();
    public ICollection<LiveOdd> LiveOdds { get; set; } = new List<LiveOdd>();
    public ICollection<TheOddsLiveOdd> TheOddsLiveOdds { get; set; } = new List<TheOddsLiveOdd>();
    public ICollection<OddsOpenClose> OddsOpenCloses { get; set; } = new List<OddsOpenClose>();
    public ICollection<OddsMovement> OddsMovements { get; set; } = new List<OddsMovement>();
    public ICollection<MarketConsensus> MarketConsensuses { get; set; } = new List<MarketConsensus>();
    public ICollection<FixtureEvent> Events { get; set; } = new List<FixtureEvent>();
    public ICollection<FixtureLineup> Lineups { get; set; } = new List<FixtureLineup>();
    public ICollection<FixtureStatistic> Statistics { get; set; } = new List<FixtureStatistic>();
    public ICollection<FixturePlayerStatistic> PlayerStatistics { get; set; } = new List<FixturePlayerStatistic>();
    public ICollection<FixturePrediction> Predictions { get; set; } = new List<FixturePrediction>();
    public ICollection<FixtureInjury> Injuries { get; set; } = new List<FixtureInjury>();
}
