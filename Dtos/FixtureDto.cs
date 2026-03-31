using SmartBets.Enums;

namespace SmartBets.Dtos;

public class FixtureDto
{
    public long Id { get; set; }
    public long ApiFixtureId { get; set; }
    public int Season { get; set; }
    public DateTime KickoffAt { get; set; }
    public string? Status { get; set; }
    public string? StatusLong { get; set; }
    public int? Elapsed { get; set; }
    public int? StatusExtra { get; set; }
    public FixtureStateBucket StateBucket { get; set; }
    public string? Referee { get; set; }
    public string? Timezone { get; set; }
    public string? VenueName { get; set; }
    public string? VenueCity { get; set; }
    public string? Round { get; set; }

    public long LeagueId { get; set; }
    public long LeagueApiId { get; set; }
    public string LeagueName { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;

    public long HomeTeamId { get; set; }
    public long HomeTeamApiId { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string? HomeTeamLogoUrl { get; set; }

    public long AwayTeamId { get; set; }
    public long AwayTeamApiId { get; set; }
    public string AwayTeamName { get; set; } = string.Empty;
    public string? AwayTeamLogoUrl { get; set; }

    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }
}
