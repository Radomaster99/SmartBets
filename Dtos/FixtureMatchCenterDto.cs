namespace SmartBets.Dtos;

public class FixtureMatchCenterDto
{
    public FixtureDetailDto Detail { get; set; } = new();
    public IReadOnlyList<FixtureEventDto> Events { get; set; } = Array.Empty<FixtureEventDto>();
    public IReadOnlyList<FixtureTeamStatisticsDto> Statistics { get; set; } = Array.Empty<FixtureTeamStatisticsDto>();
    public IReadOnlyList<FixtureTeamLineupDto> Lineups { get; set; } = Array.Empty<FixtureTeamLineupDto>();
    public IReadOnlyList<FixtureTeamPlayerStatisticsDto> Players { get; set; } = Array.Empty<FixtureTeamPlayerStatisticsDto>();
}
