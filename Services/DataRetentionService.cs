using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartBets.Data;

namespace SmartBets.Services;

public class DataRetentionService
{
    private readonly AppDbContext _dbContext;
    private readonly IOptionsMonitor<DataRetentionOptions> _optionsMonitor;

    public DataRetentionService(
        AppDbContext dbContext,
        IOptionsMonitor<DataRetentionOptions> optionsMonitor)
    {
        _dbContext = dbContext;
        _optionsMonitor = optionsMonitor;
    }

    public async Task<DataRetentionCleanupResult> CleanupAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var options = _optionsMonitor.CurrentValue;

        var syncErrorsCutoff = nowUtc.AddDays(-options.GetSyncErrorsRetentionDays());
        var liveOddsCutoff = nowUtc.AddDays(-options.GetLiveOddsRetentionDays());
        var preMatchOddsCutoff = nowUtc.AddDays(-options.GetPreMatchOddsRetentionDays());

        var oldFixtureIdsQuery = _dbContext.Fixtures
            .Where(x => x.KickoffAt < preMatchOddsCutoff)
            .Select(x => x.Id);

        var deletedSyncErrors = await _dbContext.SyncErrors
            .Where(x => x.OccurredAt < syncErrorsCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var deletedLiveOdds = await _dbContext.LiveOdds
            .Where(x => x.CollectedAtUtc < liveOddsCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var deletedPreMatchOdds = await _dbContext.PreMatchOdds
            .Where(x => oldFixtureIdsQuery.Contains(x.FixtureId))
            .ExecuteDeleteAsync(cancellationToken);

        var deletedOddsOpenClose = await _dbContext.OddsOpenCloses
            .Where(x => oldFixtureIdsQuery.Contains(x.FixtureId))
            .ExecuteDeleteAsync(cancellationToken);

        var deletedOddsMovements = await _dbContext.OddsMovements
            .Where(x => oldFixtureIdsQuery.Contains(x.FixtureId))
            .ExecuteDeleteAsync(cancellationToken);

        var deletedMarketConsensuses = await _dbContext.MarketConsensuses
            .Where(x => oldFixtureIdsQuery.Contains(x.FixtureId))
            .ExecuteDeleteAsync(cancellationToken);

        return new DataRetentionCleanupResult
        {
            ExecutedAtUtc = nowUtc,
            DeletedSyncErrors = deletedSyncErrors,
            DeletedLiveOdds = deletedLiveOdds,
            DeletedPreMatchOdds = deletedPreMatchOdds,
            DeletedOddsOpenClose = deletedOddsOpenClose,
            DeletedOddsMovements = deletedOddsMovements,
            DeletedMarketConsensuses = deletedMarketConsensuses
        };
    }
}

public class DataRetentionCleanupResult
{
    public DateTime ExecutedAtUtc { get; set; }
    public int DeletedSyncErrors { get; set; }
    public int DeletedLiveOdds { get; set; }
    public int DeletedPreMatchOdds { get; set; }
    public int DeletedOddsOpenClose { get; set; }
    public int DeletedOddsMovements { get; set; }
    public int DeletedMarketConsensuses { get; set; }
}
