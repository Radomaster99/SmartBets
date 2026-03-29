namespace SmartBets.Entities;

public class Fixture
{
    public long Id { get; set; }
    public long ApiFixtureId { get; set; }
    public long LeagueId { get; set; }
    public int Season { get; set; }
    public DateTime KickoffAt { get; set; }
    public string? Status { get; set; }
    public long HomeTeamId { get; set; }
    public long AwayTeamId { get; set; }
    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }

    public League League { get; set; } = null!;
    public Team HomeTeam { get; set; } = null!;
    public Team AwayTeam { get; set; } = null!;
    public ICollection<PreMatchOdd> PreMatchOdds { get; set; } = new List<PreMatchOdd>();
}