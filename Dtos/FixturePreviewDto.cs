namespace SmartBets.Dtos;

public class FixturePreviewDto
{
    public FixtureDetailDto Detail { get; set; } = new();
    public FixturePredictionDto? Prediction { get; set; }
    public IReadOnlyList<FixtureInjuryDto> Injuries { get; set; } = Array.Empty<FixtureInjuryDto>();
    public TeamRecentFormDto HomeRecentForm { get; set; } = new();
    public TeamRecentFormDto AwayRecentForm { get; set; } = new();
    public FixtureHeadToHeadDto HeadToHead { get; set; } = new();
}
