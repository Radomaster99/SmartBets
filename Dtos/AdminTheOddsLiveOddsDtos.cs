namespace SmartBets.Dtos;

public class AdminTheOddsFixtureRefreshResultDto
{
    public long ApiFixtureId { get; set; }
    public DateTime KickoffAtUtc { get; set; }
    public string? Status { get; set; }
    public int? Elapsed { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public bool Forced { get; set; }
    public bool ServedFromCache { get; set; }
    public bool RefreshedRemotely { get; set; }
    public bool HasCachedOdds { get; set; }
    public int MarketsReturned { get; set; }
    public TheOddsLiveOddsSyncResultDto? Sync { get; set; }
    public IReadOnlyList<LiveOddsMarketDto> Items { get; set; } = Array.Empty<LiveOddsMarketDto>();
}

public class AdminTheOddsLeagueFixtureItemDto
{
    public long ApiFixtureId { get; set; }
    public DateTime KickoffAtUtc { get; set; }
    public string? Status { get; set; }
    public int? Elapsed { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public bool HasCachedOdds { get; set; }
    public FixtureLiveOddsSummaryDto? Summary { get; set; }
}

public class AdminTheOddsLeagueRefreshResultDto
{
    public long LeagueApiId { get; set; }
    public int Season { get; set; }
    public bool Forced { get; set; }
    public bool ServedFromCache { get; set; }
    public bool RefreshedRemotely { get; set; }
    public int LiveFixturesInScope { get; set; }
    public int FixturesWithCachedOdds { get; set; }
    public int FixturesMissingCachedOdds { get; set; }
    public TheOddsLiveOddsSyncResultDto? Sync { get; set; }
    public IReadOnlyList<AdminTheOddsLeagueFixtureItemDto> Items { get; set; } = Array.Empty<AdminTheOddsLeagueFixtureItemDto>();
}
