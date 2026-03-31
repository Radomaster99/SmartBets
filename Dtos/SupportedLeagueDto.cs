namespace SmartBets.Dtos;

public class SupportedLeagueDto
{
    public long Id { get; set; }
    public long LeagueApiId { get; set; }
    public int Season { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string LeagueName { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public LeagueCoverageFlagsDto? Coverage { get; set; }
    public SupportedLeagueSyncSummaryDto Sync { get; set; } = new();
}

public class SupportedLeagueSyncSummaryDto
{
    public DateTime? TeamsLastSyncedAtUtc { get; set; }
    public DateTime? FixturesUpcomingLastSyncedAtUtc { get; set; }
    public DateTime? FixturesFullLastSyncedAtUtc { get; set; }
    public DateTime? EventsLastSyncedAtUtc { get; set; }
    public DateTime? StatisticsLastSyncedAtUtc { get; set; }
    public DateTime? LineupsLastSyncedAtUtc { get; set; }
    public DateTime? PlayerStatisticsLastSyncedAtUtc { get; set; }
    public DateTime? PredictionsLastSyncedAtUtc { get; set; }
    public DateTime? InjuriesLastSyncedAtUtc { get; set; }
    public DateTime? TeamStatisticsLastSyncedAtUtc { get; set; }
    public DateTime? RoundsLastSyncedAtUtc { get; set; }
    public DateTime? TopScorersLastSyncedAtUtc { get; set; }
    public DateTime? TopAssistsLastSyncedAtUtc { get; set; }
    public DateTime? TopCardsLastSyncedAtUtc { get; set; }
    public DateTime? StandingsLastSyncedAtUtc { get; set; }
    public DateTime? OddsLastSyncedAtUtc { get; set; }
    public DateTime? OddsAnalyticsLastSyncedAtUtc { get; set; }
    public DateTime? BookmakersLastSyncedAtUtc { get; set; }
}
