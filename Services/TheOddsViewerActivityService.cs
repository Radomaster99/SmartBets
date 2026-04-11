using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace SmartBets.Services;

public class TheOddsViewerActivityService
{
    private readonly ConcurrentDictionary<long, DateTime> _fixtureLastSeenUtc = new();
    private readonly IOptionsMonitor<TheOddsApiOptions> _optionsMonitor;

    public TheOddsViewerActivityService(IOptionsMonitor<TheOddsApiOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    public TheOddsViewerHeartbeatResult TouchFixtures(IReadOnlyCollection<long> fixtureIds)
    {
        var nowUtc = DateTime.UtcNow;
        var distinctFixtureIds = fixtureIds
            .Where(x => x > 0)
            .Distinct()
            .Take(200)
            .ToList();

        foreach (var fixtureId in distinctFixtureIds)
        {
            _fixtureLastSeenUtc[fixtureId] = nowUtc;
        }

        var ttl = _optionsMonitor.CurrentValue.GetViewerHeartbeatTtl();
        PruneExpired(nowUtc, ttl);

        return new TheOddsViewerHeartbeatResult(
            distinctFixtureIds.Count,
            _fixtureLastSeenUtc.Count,
            nowUtc,
            ttl);
    }

    public IReadOnlyList<long> GetActiveFixtureIds(int maxCount)
    {
        var nowUtc = DateTime.UtcNow;
        var ttl = _optionsMonitor.CurrentValue.GetViewerHeartbeatTtl();
        PruneExpired(nowUtc, ttl);

        return _fixtureLastSeenUtc
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Take(Math.Max(1, maxCount))
            .Select(x => x.Key)
            .ToList();
    }

    public int GetActiveFixtureCount()
    {
        PruneExpired(DateTime.UtcNow, _optionsMonitor.CurrentValue.GetViewerHeartbeatTtl());
        return _fixtureLastSeenUtc.Count;
    }

    public void ClearActiveFixtures()
    {
        _fixtureLastSeenUtc.Clear();
    }

    private void PruneExpired(DateTime nowUtc, TimeSpan ttl)
    {
        var cutoffUtc = nowUtc.Subtract(ttl);

        foreach (var entry in _fixtureLastSeenUtc)
        {
            if (entry.Value < cutoffUtc)
            {
                _fixtureLastSeenUtc.TryRemove(entry.Key, out _);
            }
        }
    }
}

public sealed record TheOddsViewerHeartbeatResult(
    int AcceptedFixtureIds,
    int ActiveFixtureIds,
    DateTime TouchedAtUtc,
    TimeSpan Ttl);
