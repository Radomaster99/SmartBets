namespace SmartBets.Dtos;

public class FixtureDto
{
    public long Id { get; set; }
    public long ApiFixtureId { get; set; }
    public int Season { get; set; }
    public DateTime KickoffAt { get; set; }
    public string? Status { get; set; }

    public long LeagueId { get; set; }
    public string LeagueName { get; set; } = string.Empty;

    public long HomeTeamId { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;

    public long AwayTeamId { get; set; }
    public string AwayTeamName { get; set; } = string.Empty;

    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }
}