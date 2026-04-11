namespace SmartBets.Dtos;

public class TheOddsViewerHeartbeatRequestDto
{
    public IReadOnlyList<long> FixtureIds { get; set; } = Array.Empty<long>();
}

public class TheOddsViewerHeartbeatResponseDto
{
    public int ReceivedFixtureIds { get; set; }
    public int AcceptedFixtureIds { get; set; }
    public int ActiveFixtureIds { get; set; }
    public DateTime TouchedAtUtc { get; set; }
    public bool ViewerDrivenRefreshEnabled { get; set; }
    public int ViewerHeartbeatTtlSeconds { get; set; }
    public bool LiveOddsHeartbeatEnabled { get; set; }
    public bool EffectiveViewerDrivenRefreshEnabled { get; set; }
    public bool HeartbeatAccepted { get; set; }
}

public class TheOddsViewerConfigDto
{
    public bool ViewerDrivenRefreshEnabled { get; set; }
    public bool LiveOddsHeartbeatEnabled { get; set; }
    public bool TheOddsProviderEnabled { get; set; }
    public bool TheOddsProviderConfigured { get; set; }
    public bool ConfigViewerDrivenRefreshEnabled { get; set; }
    public bool EffectiveViewerDrivenRefreshEnabled { get; set; }
    public bool ReadDrivenCatchUpEnabled { get; set; }
    public int ViewerHeartbeatTtlSeconds { get; set; }
    public int ViewerRefreshIntervalSeconds { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
