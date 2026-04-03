namespace SmartBets.Dtos;

public class LiveBetTypeDto
{
    public long ApiBetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime SyncedAtUtc { get; set; }
}

public class LiveOddsValueDto
{
    public string OutcomeLabel { get; set; } = string.Empty;
    public string? Line { get; set; }
    public decimal? Odd { get; set; }
    public bool? IsMain { get; set; }
    public bool? Stopped { get; set; }
    public bool? Blocked { get; set; }
    public bool? Finished { get; set; }
}

public class LiveOddsMarketDto
{
    public long FixtureId { get; set; }
    public long ApiFixtureId { get; set; }
    public long BookmakerId { get; set; }
    public long ApiBookmakerId { get; set; }
    public string Bookmaker { get; set; } = string.Empty;
    public long ApiBetId { get; set; }
    public string BetName { get; set; } = string.Empty;
    public DateTime CollectedAtUtc { get; set; }
    public DateTime LastSnapshotCollectedAtUtc { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
    public IReadOnlyList<LiveOddsValueDto> Values { get; set; } = Array.Empty<LiveOddsValueDto>();
}

public class LiveOddsSyncResultDto
{
    public long? FixtureApiId { get; set; }
    public long? LeagueApiId { get; set; }
    public long? BetApiId { get; set; }
    public long? BookmakerApiId { get; set; }
    public int ProviderFixturesReceived { get; set; }
    public int ProviderBookmakersReceived { get; set; }
    public bool ProviderReturnedEmpty { get; set; }
    public bool UsedLeagueFallback { get; set; }
    public long? FallbackLeagueApiId { get; set; }
    public int LocalFixturesResolved { get; set; }
    public IReadOnlyList<long> ProviderFixtureApiIdsSample { get; set; } = Array.Empty<long>();
    public IReadOnlyList<long> MissingFixtureApiIdsSample { get; set; } = Array.Empty<long>();
    public int FixturesMatched { get; set; }
    public int FixturesMissingInDatabase { get; set; }
    public int BookmakersProcessed { get; set; }
    public int BookmakersInserted { get; set; }
    public int BookmakersUpdated { get; set; }
    public int BetsProcessed { get; set; }
    public int SnapshotsProcessed { get; set; }
    public int SnapshotsInserted { get; set; }
    public int SnapshotsSkippedUnchanged { get; set; }
    public DateTime ExecutedAtUtc { get; set; }
}

public class LiveBetTypesSyncResultDto
{
    public int Processed { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public DateTime ExecutedAtUtc { get; set; }
}
