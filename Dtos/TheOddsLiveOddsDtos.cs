namespace SmartBets.Dtos;

public class TheOddsLiveOddsSyncResultDto
{
    public long? ApiFixtureId { get; set; }
    public long? LeagueApiId { get; set; }
    public int? Season { get; set; }
    public string? SportKey { get; set; }
    public string SourceProvider { get; set; } = "the-odds-api";
    public string Regions { get; set; } = string.Empty;
    public string MarketKey { get; set; } = string.Empty;
    public bool ProviderEnabled { get; set; }
    public bool ProviderConfigured { get; set; }
    public string? SkippedReason { get; set; }
    public int RequestsUsed { get; set; }
    public int ProviderEventsReceived { get; set; }
    public int FixturesMatched { get; set; }
    public int FixturesMissingMatch { get; set; }
    public int BookmakersProcessed { get; set; }
    public int SnapshotsProcessed { get; set; }
    public int SnapshotsInserted { get; set; }
    public int SnapshotsSkippedUnchanged { get; set; }
    public DateTime ExecutedAtUtc { get; set; }
}
