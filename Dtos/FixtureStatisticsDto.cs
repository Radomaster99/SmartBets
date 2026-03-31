namespace SmartBets.Dtos;

public class FixtureStatisticValueDto
{
    public int SortOrder { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Value { get; set; }
}

public class FixtureTeamStatisticsDto
{
    public long? TeamId { get; set; }
    public long TeamApiId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamLogoUrl { get; set; }
    public DateTime? SyncedAtUtc { get; set; }
    public IReadOnlyList<FixtureStatisticValueDto> Statistics { get; set; } = Array.Empty<FixtureStatisticValueDto>();
}
