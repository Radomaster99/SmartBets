namespace SmartBets.Dtos;

public class AdminTheOddsViewerRefreshUpdateRequestDto
{
    public bool LiveOddsHeartbeatEnabled { get; set; }
}

public class AdminTheOddsViewerRefreshStateDto
{
    public bool LiveOddsHeartbeatEnabled { get; set; }
    public bool TheOddsProviderEnabled { get; set; }
    public bool TheOddsProviderConfigured { get; set; }
    public bool ConfigViewerDrivenRefreshEnabled { get; set; }
    public bool EffectiveViewerDrivenRefreshEnabled { get; set; }
    public bool ReadDrivenCatchUpEnabled { get; set; }
    public int ViewerHeartbeatTtlSeconds { get; set; }
    public int ViewerRefreshIntervalSeconds { get; set; }
    public int ActiveFixtureIds { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
