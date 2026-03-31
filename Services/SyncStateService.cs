using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Entities;

namespace SmartBets.Services;

public class SyncStateService
{
    private readonly AppDbContext _dbContext;

    public SyncStateService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SetLastSyncedAtAsync(
        string entityType,
        long? leagueApiId,
        int? season,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.SyncStates
            .FirstOrDefaultAsync(
                x => x.EntityType == entityType &&
                     x.LeagueApiId == leagueApiId &&
                     x.Season == season,
                cancellationToken);

        if (existing is null)
        {
            existing = new SyncState
            {
                EntityType = entityType,
                LeagueApiId = leagueApiId,
                Season = season,
                LastSyncedAt = syncedAtUtc
            };

            _dbContext.SyncStates.Add(existing);
        }
        else
        {
            existing.LastSyncedAt = syncedAtUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetLastSyncedAtBatchAsync(
        IEnumerable<SyncStateUpsertItem> items,
        CancellationToken cancellationToken = default)
    {
        var normalizedItems = items
            .Where(x => !string.IsNullOrWhiteSpace(x.EntityType))
            .GroupBy(x => BuildKey(x.EntityType, x.LeagueApiId, x.Season), StringComparer.Ordinal)
            .Select(x => x
                .OrderByDescending(y => y.SyncedAtUtc)
                .First())
            .ToList();

        if (normalizedItems.Count == 0)
            return;

        var entityTypes = normalizedItems
            .Select(x => x.EntityType.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var existing = await _dbContext.SyncStates
            .Where(x => entityTypes.Contains(x.EntityType))
            .ToListAsync(cancellationToken);

        var existingByKey = existing.ToDictionary(
            x => BuildKey(x.EntityType, x.LeagueApiId, x.Season),
            x => x,
            StringComparer.Ordinal);

        foreach (var item in normalizedItems)
        {
            var key = BuildKey(item.EntityType, item.LeagueApiId, item.Season);

            if (existingByKey.TryGetValue(key, out var state))
            {
                state.LastSyncedAt = item.SyncedAtUtc;
                continue;
            }

            state = new SyncState
            {
                EntityType = item.EntityType.Trim(),
                LeagueApiId = item.LeagueApiId,
                Season = item.Season,
                LastSyncedAt = item.SyncedAtUtc
            };

            _dbContext.SyncStates.Add(state);
            existingByKey[key] = state;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string BuildKey(string entityType, long? leagueApiId, int? season)
    {
        return $"{entityType.Trim()}:{leagueApiId?.ToString() ?? "global"}:{season?.ToString() ?? "global"}";
    }
}

public class SyncStateUpsertItem
{
    public string EntityType { get; set; } = string.Empty;
    public long? LeagueApiId { get; set; }
    public int? Season { get; set; }
    public DateTime SyncedAtUtc { get; set; }
}
