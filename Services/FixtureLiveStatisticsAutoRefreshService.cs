using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using SmartBets.Entities;
using SmartBets.Enums;

namespace SmartBets.Services;

public class FixtureLiveStatisticsAutoRefreshService
{
    private static readonly TimeSpan DuplicateAttemptWindow = TimeSpan.FromSeconds(10);

    private readonly FixtureMatchCenterSyncService _fixtureMatchCenterSyncService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<FixtureLiveStatisticsAutoRefreshService> _logger;
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _fixtureGates = new();

    public FixtureLiveStatisticsAutoRefreshService(
        FixtureMatchCenterSyncService fixtureMatchCenterSyncService,
        IMemoryCache memoryCache,
        ILogger<FixtureLiveStatisticsAutoRefreshService> logger)
    {
        _fixtureMatchCenterSyncService = fixtureMatchCenterSyncService;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<bool> TryRefreshAsync(Fixture fixture, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        if (fixture.ApiFixtureId <= 0)
            return false;

        if (FixtureStatusMapper.GetStateBucket(fixture.Status) != FixtureStateBucket.Live)
            return false;

        var cacheKey = BuildCacheKey(fixture.ApiFixtureId);
        if (_memoryCache.TryGetValue(cacheKey, out _))
            return false;

        var gate = _fixtureGates.GetOrAdd(fixture.ApiFixtureId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            if (_memoryCache.TryGetValue(cacheKey, out _))
                return false;

            _memoryCache.Set(cacheKey, true, DuplicateAttemptWindow);

            var result = await _fixtureMatchCenterSyncService.SyncFixtureStatisticsAsync(
                fixture.ApiFixtureId,
                force: false,
                cancellationToken);

            return result.StatisticsSynced;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Live statistics auto-refresh failed for fixture {ApiFixtureId}. Returning stored snapshot.",
                fixture.ApiFixtureId);

            return false;
        }
        finally
        {
            gate.Release();
        }
    }

    private static string BuildCacheKey(long apiFixtureId)
    {
        return $"fixture-live-statistics-refresh:{apiFixtureId}";
    }
}
