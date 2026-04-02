namespace SmartBets.Dtos;

public class SyncStatusDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public ApiQuotaStatusDto? ApiQuota { get; set; }
    public CoreAutomationStatusDto? CoreAutomation { get; set; }
    public IReadOnlyList<GlobalSyncStatusItemDto> Global { get; set; } = Array.Empty<GlobalSyncStatusItemDto>();
    public IReadOnlyList<LeagueSyncStatusItemDto> Leagues { get; set; } = Array.Empty<LeagueSyncStatusItemDto>();
}

public class GlobalSyncStatusItemDto
{
    public string EntityType { get; set; } = string.Empty;
    public DateTime? LastSyncedAtUtc { get; set; }
}

public class LeagueSyncStatusItemDto
{
    public long LeagueApiId { get; set; }
    public int Season { get; set; }
    public string LeagueName { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public DateTime? TeamsLastSyncedAtUtc { get; set; }
    public DateTime? FixturesLiveLastSyncedAtUtc { get; set; }
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
    public DateTime? LiveOddsLastSyncedAtUtc { get; set; }
    public DateTime? OddsAnalyticsLastSyncedAtUtc { get; set; }
    public DateTime? BookmakersLastSyncedAtUtc { get; set; }
}
