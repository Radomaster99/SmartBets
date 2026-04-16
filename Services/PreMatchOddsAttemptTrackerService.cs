namespace SmartBets.Services;

public sealed class PreMatchOddsAttemptTrackerService
{
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(3);
    private readonly object _sync = new();
    private readonly Dictionary<long, DateTime> _lastAttemptedAtByApiFixtureId = new();

    public IReadOnlyDictionary<long, DateTime> GetLastAttemptedAtLookup(
        IReadOnlyCollection<long> apiFixtureIds,
        DateTime nowUtc)
    {
        if (apiFixtureIds.Count == 0)
            return new Dictionary<long, DateTime>();

        lock (_sync)
        {
            Prune(nowUtc);

            return apiFixtureIds
                .Where(x => _lastAttemptedAtByApiFixtureId.ContainsKey(x))
                .ToDictionary(x => x, x => _lastAttemptedAtByApiFixtureId[x]);
        }
    }

    public void RecordAttempt(long apiFixtureId, DateTime attemptedAtUtc)
    {
        if (apiFixtureId <= 0)
            return;

        lock (_sync)
        {
            Prune(attemptedAtUtc);
            _lastAttemptedAtByApiFixtureId[apiFixtureId] = attemptedAtUtc;
        }
    }

    private void Prune(DateTime nowUtc)
    {
        var cutoff = nowUtc - RetentionWindow;
        var expiredKeys = _lastAttemptedAtByApiFixtureId
            .Where(x => x.Value < cutoff)
            .Select(x => x.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _lastAttemptedAtByApiFixtureId.Remove(key);
        }
    }
}
