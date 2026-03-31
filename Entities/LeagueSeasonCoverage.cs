namespace SmartBets.Entities;

public class LeagueSeasonCoverage
{
    public long Id { get; set; }
    public long LeagueApiId { get; set; }
    public int Season { get; set; }

    public bool HasFixtures { get; set; }
    public bool HasFixtureEvents { get; set; }
    public bool HasLineups { get; set; }
    public bool HasFixtureStatistics { get; set; }
    public bool HasPlayerStatistics { get; set; }

    public bool HasStandings { get; set; }
    public bool HasPlayers { get; set; }
    public bool HasTopScorers { get; set; }
    public bool HasTopAssists { get; set; }
    public bool HasTopCards { get; set; }
    public bool HasInjuries { get; set; }
    public bool HasPredictions { get; set; }
    public bool HasOdds { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
