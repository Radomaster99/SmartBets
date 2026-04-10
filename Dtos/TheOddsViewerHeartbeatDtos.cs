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
    public int ViewerHeartbeatTtlSeconds { get; set; }
}
