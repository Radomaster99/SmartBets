using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartBets.Data;
using SmartBets.Enums;

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
        var liveOddsFinishedFixtureCutoff = nowUtc.AddHours(-options.GetLiveOddsFinishedFixtureRetentionHours());
        var preMatchOddsCutoff = nowUtc.AddDays(-options.GetPreMatchOddsRetentionDays());
        var derivedOddsCutoff = nowUtc.AddDays(-options.GetDerivedOddsAnalyticsRetentionDays());

        var liveStatuses = FixtureStatusMapper
            .GetStatusesForBucket(FixtureStateBucket.Live)
            .ToList();

        var oldFixtureIdsQuery = _dbContext.Fixtures
            .Where(x => x.KickoffAt < preMatchOddsCutoff)
            .Select(x => x.Id);

        var oldDerivedFixtureIdsQuery = _dbContext.Fixtures
            .Where(x => x.KickoffAt < derivedOddsCutoff)
            .Select(x => x.Id);

        var staleFinishedLiveFixtureIdsQuery = _dbContext.Fixtures
            .Where(x => x.KickoffAt < liveOddsFinishedFixtureCutoff)
            .Where(x => x.Status == null || !liveStatuses.Contains(x.Status))
            .Select(x => x.Id);

        var matchWinnerAliases = LiveMatchWinnerMarket.GetUppercaseAliases();

        var deletedSyncErrors = await _dbContext.SyncErrors
            .Where(x => x.OccurredAt < syncErrorsCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var normalizedLegacyMatchWinnerLiveOdds = await _dbContext.LiveOdds
            .Where(x => matchWinnerAliases.Contains(x.BetName.ToUpper()) && x.BetName != PreMatchOddsService.DefaultMarketName)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.BetName, PreMatchOddsService.DefaultMarketName), cancellationToken);

        var deletedNonMatchWinnerLiveOdds = await _dbContext.LiveOdds
            .Where(x =>
                !matchWinnerAliases.Contains(x.BetName.ToUpper()) &&
                !EF.Functions.ILike(x.BetName, LiveMatchWinnerMarket.Contains1X2IlikePattern))
            .ExecuteDeleteAsync(cancellationToken);

        var deletedLiveOdds = await _dbContext.LiveOdds
            .Where(x =>
                x.CollectedAtUtc < liveOddsCutoff ||
                staleFinishedLiveFixtureIdsQuery.Contains(x.FixtureId))
            .ExecuteDeleteAsync(cancellationToken);

        var deletedTheOddsLiveOdds = await _dbContext.TheOddsLiveOdds
            .Where(x =>
                x.CollectedAtUtc < liveOddsCutoff ||
                staleFinishedLiveFixtureIdsQuery.Contains(x.FixtureId))
            .ExecuteDeleteAsync(cancellationToken);

        var deletedPreMatchOdds = await _dbContext.PreMatchOdds
            .Where(x => oldFixtureIdsQuery.Contains(x.FixtureId))
            .ExecuteDeleteAsync(cancellationToken);

        var deletedOddsOpenClose = await _dbContext.OddsOpenCloses
            .Where(x => oldDerivedFixtureIdsQuery.Contains(x.FixtureId))
            .ExecuteDeleteAsync(cancellationToken);

        var deletedOddsMovements = await _dbContext.OddsMovements
            .Where(x => oldDerivedFixtureIdsQuery.Contains(x.FixtureId))
            .ExecuteDeleteAsync(cancellationToken);

        var deletedMarketConsensuses = await _dbContext.MarketConsensuses
            .Where(x => oldDerivedFixtureIdsQuery.Contains(x.FixtureId))
            .ExecuteDeleteAsync(cancellationToken);

        return new DataRetentionCleanupResult
        {
            ExecutedAtUtc = nowUtc,
            DeletedSyncErrors = deletedSyncErrors,
            NormalizedLegacyMatchWinnerLiveOdds = normalizedLegacyMatchWinnerLiveOdds,
            DeletedNonMatchWinnerLiveOdds = deletedNonMatchWinnerLiveOdds,
            DeletedLiveOdds = deletedLiveOdds,
            DeletedTheOddsLiveOdds = deletedTheOddsLiveOdds,
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
    public int NormalizedLegacyMatchWinnerLiveOdds { get; set; }
    public int DeletedNonMatchWinnerLiveOdds { get; set; }
    public int DeletedLiveOdds { get; set; }
    public int DeletedTheOddsLiveOdds { get; set; }
    public int DeletedPreMatchOdds { get; set; }
    public int DeletedOddsOpenClose { get; set; }
    public int DeletedOddsMovements { get; set; }
    public int DeletedMarketConsensuses { get; set; }
}
