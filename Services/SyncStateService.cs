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
}