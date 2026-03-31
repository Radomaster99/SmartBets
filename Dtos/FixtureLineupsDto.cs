namespace SmartBets.Dtos;

public class FixtureLineupCoachDto
{
    public long? ApiCoachId { get; set; }
    public string? Name { get; set; }
    public string? PhotoUrl { get; set; }
}

public class FixtureLineupPlayerDto
{
    public int SortOrder { get; set; }
    public long? ApiPlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Number { get; set; }
    public string? Position { get; set; }
    public string? Grid { get; set; }
}

public class FixtureTeamLineupDto
{
    public long? TeamId { get; set; }
    public long TeamApiId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamLogoUrl { get; set; }
    public string? Formation { get; set; }
    public FixtureLineupCoachDto Coach { get; set; } = new();
    public DateTime? SyncedAtUtc { get; set; }
    public IReadOnlyList<FixtureLineupPlayerDto> StartXI { get; set; } = Array.Empty<FixtureLineupPlayerDto>();
    public IReadOnlyList<FixtureLineupPlayerDto> Substitutes { get; set; } = Array.Empty<FixtureLineupPlayerDto>();
}
