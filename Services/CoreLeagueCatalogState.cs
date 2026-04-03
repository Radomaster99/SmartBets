namespace SmartBets.Services;

public sealed class CoreLeagueCatalogState
{
    private readonly object _sync = new();
    private IReadOnlyList<CoreLeagueSeasonTarget> _targets = Array.Empty<CoreLeagueSeasonTarget>();
    private DateTime? _lastRefreshedAtUtc;

    public DateTime? GetLastRefreshedAtUtc()
    {
        lock (_sync)
        {
            return _lastRefreshedAtUtc;
        }
    }

    public IReadOnlyList<CoreLeagueSeasonTarget> GetTargets()
    {
        lock (_sync)
        {
            return _targets;
        }
    }

    public void ReplaceTargets(IEnumerable<CoreLeagueSeasonTarget> targets, DateTime refreshedAtUtc)
    {
        lock (_sync)
        {
            _targets = targets
                .OrderBy(x => x.CountryName)
                .ThenBy(x => x.LeagueName)
                .ThenBy(x => x.Season)
                .ToList();

            _lastRefreshedAtUtc = refreshedAtUtc;
        }
    }
}

public sealed class CoreLeagueSeasonTarget
{
    public long LeagueApiId { get; set; }
    public int Season { get; set; }
    public string LeagueName { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public bool HasFixtures { get; set; }
    public bool HasStandings { get; set; }
    public bool HasOdds { get; set; }
}
