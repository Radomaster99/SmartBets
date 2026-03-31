namespace SmartBets.Dtos;

public class SyncStatusDto
{
    public DateTime GeneratedAtUtc { get; set; }
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
    public DateTime? FixturesUpcomingLastSyncedAtUtc { get; set; }
    public DateTime? FixturesFullLastSyncedAtUtc { get; set; }
    public DateTime? StandingsLastSyncedAtUtc { get; set; }
    public DateTime? OddsLastSyncedAtUtc { get; set; }
    public DateTime? BookmakersLastSyncedAtUtc { get; set; }
}
