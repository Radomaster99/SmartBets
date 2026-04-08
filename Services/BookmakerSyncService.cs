using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Entities;

namespace SmartBets.Services;

public class BookmakerSyncResult
{
    public int Processed { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int PreMatchOddsReferences { get; set; }
    public int LiveOddsReferences { get; set; }
    public int LiveOddsRowsReassigned { get; set; }
    public int SyntheticRowsDeleted { get; set; }
    public int RemoteCallsMade { get; set; }
    public string Source { get; set; } = "local_cache";
}

public class BookmakerSyncService
{
    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _footballApiService;
    private readonly LeagueCoverageService _leagueCoverageService;

    public BookmakerSyncService(
        AppDbContext dbContext,
        FootballApiService footballApiService,
        LeagueCoverageService leagueCoverageService)
    {
        _dbContext = dbContext;
        _footballApiService = footballApiService;
        _leagueCoverageService = leagueCoverageService;
    }

    public async Task<BookmakerSyncResult> SyncReferenceBookmakersAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var source = await _footballApiService.GetOddsBookmakersAsync(cancellationToken);
        var existingBookmakers = await _dbContext.Bookmakers.ToListAsync(cancellationToken);
        var existingByApiId = existingBookmakers.ToDictionary(x => x.ApiBookmakerId);

        var result = new BookmakerSyncResult
        {
            Source = "reference_endpoint",
            RemoteCallsMade = 1
        };

        foreach (var item in source)
        {
            var normalizedName = item.Name.Trim();

            if (existingByApiId.TryGetValue(item.Id, out var existing))
            {
                if (!string.Equals(existing.Name, normalizedName, StringComparison.Ordinal))
                {
                    existing.Name = normalizedName;
                    result.Updated++;
                }
            }
            else
            {
                _dbContext.Bookmakers.Add(new Bookmaker
                {
                    ApiBookmakerId = item.Id,
                    Name = normalizedName
                });

                result.Inserted++;
            }

            result.Processed++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
        return result;
    }

    public async Task<BookmakerSyncResult> SyncBookmakersAsync(long leagueId, int season, CancellationToken cancellationToken = default)
    {
        await _leagueCoverageService.EnsureOddsSupportedAsync(leagueId, season, cancellationToken);

        var leagueExists = await _dbContext.Leagues
            .AnyAsync(x => x.ApiLeagueId == leagueId && x.Season == season, cancellationToken);

        if (!leagueExists)
        {
            throw new InvalidOperationException($"League with apiLeagueId {leagueId} and season {season} was not found in database.");
        }

        var result = new BookmakerSyncResult();

        var scopedFixtureIds = await _dbContext.Fixtures
            .Where(x => x.League.ApiLeagueId == leagueId && x.Season == season)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (scopedFixtureIds.Count == 0)
            return result;

        var preMatchBookmakerIds = await _dbContext.PreMatchOdds
            .AsNoTracking()
            .Where(x => scopedFixtureIds.Contains(x.FixtureId))
            .Select(x => x.BookmakerId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var liveOddsBookmakerIds = await _dbContext.LiveOdds
            .AsNoTracking()
            .Where(x => scopedFixtureIds.Contains(x.FixtureId))
            .Select(x => x.BookmakerId)
            .Distinct()
            .ToListAsync(cancellationToken);

        result.PreMatchOddsReferences = preMatchBookmakerIds.Count;
        result.LiveOddsReferences = liveOddsBookmakerIds.Count;
        result.Processed = preMatchBookmakerIds
            .Concat(liveOddsBookmakerIds)
            .Distinct()
            .Count();

        var syntheticBookmaker = await _dbContext.Bookmakers
            .FirstOrDefaultAsync(
                x => x.ApiBookmakerId == SingleSourceLiveBookmakerIdentity.SyntheticApiBookmakerId,
                cancellationToken);

        var realSingleSourceBookmaker = await _dbContext.Bookmakers
            .AsNoTracking()
            .Where(x =>
                x.ApiBookmakerId > SingleSourceLiveBookmakerIdentity.SyntheticApiBookmakerId &&
                SingleSourceLiveBookmakerIdentity.IsSingleSourceName(x.Name))
            .OrderBy(x => x.ApiBookmakerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (syntheticBookmaker is not null &&
            !SingleSourceLiveBookmakerIdentity.IsSingleSourceName(syntheticBookmaker.Name))
        {
            syntheticBookmaker.Name = SingleSourceLiveBookmakerIdentity.Name;
            result.Updated++;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (syntheticBookmaker is not null &&
            realSingleSourceBookmaker is not null &&
            syntheticBookmaker.Id != realSingleSourceBookmaker.Id)
        {
            result.LiveOddsRowsReassigned = await _dbContext.LiveOdds
                .Where(x =>
                    x.BookmakerId == syntheticBookmaker.Id &&
                    scopedFixtureIds.Contains(x.FixtureId))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.BookmakerId, realSingleSourceBookmaker.Id),
                    cancellationToken);

            if (result.LiveOddsRowsReassigned > 0)
            {
                result.Updated++;
            }

            var syntheticStillReferenced =
                await _dbContext.LiveOdds.AnyAsync(x => x.BookmakerId == syntheticBookmaker.Id, cancellationToken) ||
                await _dbContext.PreMatchOdds.AnyAsync(x => x.BookmakerId == syntheticBookmaker.Id, cancellationToken) ||
                await _dbContext.OddsOpenCloses.AnyAsync(x => x.BookmakerId == syntheticBookmaker.Id, cancellationToken) ||
                await _dbContext.OddsMovements.AnyAsync(x => x.BookmakerId == syntheticBookmaker.Id, cancellationToken) ||
                await _dbContext.MarketConsensuses.AnyAsync(
                    x =>
                        x.BestHomeBookmakerId == syntheticBookmaker.Id ||
                        x.BestDrawBookmakerId == syntheticBookmaker.Id ||
                        x.BestAwayBookmakerId == syntheticBookmaker.Id,
                    cancellationToken);

            if (!syntheticStillReferenced)
            {
                _dbContext.Bookmakers.Remove(syntheticBookmaker);
                await _dbContext.SaveChangesAsync(cancellationToken);
                result.SyntheticRowsDeleted = 1;
            }
        }

        return result;
    }
}
