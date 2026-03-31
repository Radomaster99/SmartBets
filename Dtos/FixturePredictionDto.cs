namespace SmartBets.Dtos;

public class FixturePredictionComparisonPairDto
{
    public string? Home { get; set; }
    public string? Away { get; set; }
}

public class FixturePredictionComparisonDto
{
    public FixturePredictionComparisonPairDto Form { get; set; } = new();
    public FixturePredictionComparisonPairDto Attack { get; set; } = new();
    public FixturePredictionComparisonPairDto Defence { get; set; } = new();
    public FixturePredictionComparisonPairDto PoissonDistribution { get; set; } = new();
    public FixturePredictionComparisonPairDto HeadToHead { get; set; } = new();
    public FixturePredictionComparisonPairDto Goals { get; set; } = new();
    public FixturePredictionComparisonPairDto Total { get; set; } = new();
}

public class FixturePredictionDto
{
    public long? WinnerTeamApiId { get; set; }
    public string? WinnerTeamName { get; set; }
    public string? WinnerComment { get; set; }
    public bool? WinOrDraw { get; set; }
    public string? UnderOver { get; set; }
    public string? Advice { get; set; }
    public string? GoalsHome { get; set; }
    public string? GoalsAway { get; set; }
    public string? PercentHome { get; set; }
    public string? PercentDraw { get; set; }
    public string? PercentAway { get; set; }
    public FixturePredictionComparisonDto Comparison { get; set; } = new();
    public DateTime SyncedAtUtc { get; set; }
}
