namespace SmartBets.Dtos;

public class LeagueRoundDto
{
    public string RoundName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime SyncedAtUtc { get; set; }
}

public class LeagueTopPlayerDto
{
    public int Rank { get; set; }
    public long ApiPlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string? PlayerPhotoUrl { get; set; }
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
    public int? PassesKey { get; set; }
    public int? ChancesCreated { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public DateTime SyncedAtUtc { get; set; }
}

public class LeagueDashboardDto
{
    public long LeagueApiId { get; set; }
    public string LeagueName { get; set; } = string.Empty;
    public int Season { get; set; }
    public string? CurrentRound { get; set; }
    public IReadOnlyList<LeagueRoundDto> Rounds { get; set; } = Array.Empty<LeagueRoundDto>();
    public IReadOnlyList<LeagueTopPlayerDto> TopScorers { get; set; } = Array.Empty<LeagueTopPlayerDto>();
    public IReadOnlyList<LeagueTopPlayerDto> TopAssists { get; set; } = Array.Empty<LeagueTopPlayerDto>();
    public IReadOnlyList<LeagueTopPlayerDto> TopCards { get; set; } = Array.Empty<LeagueTopPlayerDto>();
}

public class TeamStatisticsSyncItemDto
{
    public long ApiTeamId { get; set; }
    public bool Synced { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class TeamStatisticsSyncResultDto
{
    public long LeagueApiId { get; set; }
    public int Season { get; set; }
    public bool Forced { get; set; }
    public int TeamsConsidered { get; set; }
    public int TeamsSynced { get; set; }
    public int TeamsSkippedFresh { get; set; }
    public DateTime ExecutedAtUtc { get; set; }
    public IReadOnlyList<TeamStatisticsSyncItemDto> Items { get; set; } = Array.Empty<TeamStatisticsSyncItemDto>();
}

public class LeagueAnalyticsSyncResultDto
{
    public long LeagueApiId { get; set; }
    public int Season { get; set; }
    public bool Forced { get; set; }
    public bool RoundsSynced { get; set; }
    public bool TopScorersSynced { get; set; }
    public bool TopAssistsSynced { get; set; }
    public bool TopCardsSynced { get; set; }
    public IReadOnlyList<string> SkippedComponents { get; set; } = Array.Empty<string>();
    public DateTime ExecutedAtUtc { get; set; }
}

public class LeagueAnalyticsBatchSyncResultDto
{
    public int LeaguesConsidered { get; set; }
    public int LeaguesSynced { get; set; }
    public bool Forced { get; set; }
    public DateTime ExecutedAtUtc { get; set; }
    public IReadOnlyList<LeagueAnalyticsSyncResultDto> Items { get; set; } = Array.Empty<LeagueAnalyticsSyncResultDto>();
}
