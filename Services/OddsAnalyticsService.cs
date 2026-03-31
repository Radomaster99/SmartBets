using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Entities;
using SmartBets.Enums;

namespace SmartBets.Services;

public class OddsAnalyticsService
{
    private readonly AppDbContext _dbContext;
    private readonly SyncStateService _syncStateService;

    public OddsAnalyticsService(
        AppDbContext dbContext,
        SyncStateService syncStateService)
    {
        _dbContext = dbContext;
        _syncStateService = syncStateService;
    }

    public async Task<OddsAnalyticsRebuildResultDto> RebuildAnalyticsAsync(
        long? leagueId = null,
        int? season = null,
        long? apiFixtureId = null,
        string? marketName = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedMarketName = NormalizeMarketName(marketName);

        var fixturesQuery = _dbContext.Fixtures
            .AsNoTracking()
            .Include(x => x.League)
            .AsQueryable();

        if (apiFixtureId.HasValue)
        {
            fixturesQuery = fixturesQuery.Where(x => x.ApiFixtureId == apiFixtureId.Value);
        }

        if (leagueId.HasValue)
        {
            fixturesQuery = fixturesQuery.Where(x => x.League.ApiLeagueId == leagueId.Value);
        }

        if (season.HasValue)
        {
            fixturesQuery = fixturesQuery.Where(x => x.Season == season.Value);
        }

        var fixtures = await fixturesQuery
            .Select(x => new FixtureAnalyticsScope
            {
                FixtureId = x.Id,
                ApiFixtureId = x.ApiFixtureId,
                LeagueApiId = x.League.ApiLeagueId,
                Season = x.Season,
                KickoffAtUtc = x.KickoffAt,
                Status = x.Status
            })
            .ToListAsync(cancellationToken);

        return await RebuildForFixturesAsync(fixtures, normalizedMarketName, cancellationToken);
    }

    public async Task<OddsAnalyticsRebuildResultDto> RebuildForFixtureIdsAsync(
        IReadOnlyCollection<long> fixtureIds,
        string? marketName = null,
        CancellationToken cancellationToken = default)
    {
        if (fixtureIds.Count == 0)
        {
            return new OddsAnalyticsRebuildResultDto
            {
                MarketName = NormalizeMarketName(marketName),
                ExecutedAtUtc = DateTime.UtcNow
            };
        }

        var normalizedMarketName = NormalizeMarketName(marketName);

        var fixtures = await _dbContext.Fixtures
            .AsNoTracking()
            .Include(x => x.League)
            .Where(x => fixtureIds.Contains(x.Id))
            .Select(x => new FixtureAnalyticsScope
            {
                FixtureId = x.Id,
                ApiFixtureId = x.ApiFixtureId,
                LeagueApiId = x.League.ApiLeagueId,
                Season = x.Season,
                KickoffAtUtc = x.KickoffAt,
                Status = x.Status
            })
            .ToListAsync(cancellationToken);

        return await RebuildForFixturesAsync(fixtures, normalizedMarketName, cancellationToken);
    }

    public async Task<FixtureOddsHistoryDto?> GetHistoryAsync(
        long apiFixtureId,
        string? marketName = null,
        CancellationToken cancellationToken = default)
    {
        var fixture = await ResolveFixtureScopeAsync(apiFixtureId, cancellationToken);
        if (fixture is null)
            return null;

        var normalizedMarketName = NormalizeMarketName(marketName);

        var snapshots = await _dbContext.PreMatchOdds
            .AsNoTracking()
            .Where(x => x.FixtureId == fixture.FixtureId && x.MarketName == normalizedMarketName)
            .Select(x => new
            {
                x.BookmakerId,
                x.Bookmaker.ApiBookmakerId,
                BookmakerName = x.Bookmaker.Name,
                x.CollectedAt,
                x.HomeOdd,
                x.DrawOdd,
                x.AwayOdd
            })
            .OrderBy(x => x.BookmakerName)
            .ThenBy(x => x.CollectedAt)
            .ToListAsync(cancellationToken);

        if (snapshots.Count == 0)
            return null;

        var openCloseLookup = await _dbContext.OddsOpenCloses
            .AsNoTracking()
            .Where(x => x.FixtureId == fixture.FixtureId && x.MarketName == normalizedMarketName)
            .ToDictionaryAsync(x => x.BookmakerId, cancellationToken);

        var series = snapshots
            .GroupBy(x => new { x.BookmakerId, x.ApiBookmakerId, x.BookmakerName })
            .Select(group =>
            {
                openCloseLookup.TryGetValue(group.Key.BookmakerId, out var openClose);

                return new OddsHistorySeriesDto
                {
                    BookmakerId = group.Key.BookmakerId,
                    ApiBookmakerId = group.Key.ApiBookmakerId,
                    Bookmaker = group.Key.BookmakerName,
                    MarketName = normalizedMarketName,
                    SnapshotCount = group.Count(),
                    OpeningHomeOdd = openClose?.OpeningHomeOdd,
                    OpeningDrawOdd = openClose?.OpeningDrawOdd,
                    OpeningAwayOdd = openClose?.OpeningAwayOdd,
                    LatestHomeOdd = openClose?.LatestHomeOdd,
                    LatestDrawOdd = openClose?.LatestDrawOdd,
                    LatestAwayOdd = openClose?.LatestAwayOdd,
                    PeakHomeOdd = openClose?.PeakHomeOdd,
                    PeakDrawOdd = openClose?.PeakDrawOdd,
                    PeakAwayOdd = openClose?.PeakAwayOdd,
                    ClosingHomeOdd = openClose?.ClosingHomeOdd,
                    ClosingDrawOdd = openClose?.ClosingDrawOdd,
                    ClosingAwayOdd = openClose?.ClosingAwayOdd,
                    Points = group.Select(x => new OddsHistoryPointDto
                    {
                        CollectedAtUtc = x.CollectedAt,
                        HomeOdd = x.HomeOdd,
                        DrawOdd = x.DrawOdd,
                        AwayOdd = x.AwayOdd
                    }).ToList()
                };
            })
            .OrderBy(x => x.Bookmaker)
            .ToList();

        return new FixtureOddsHistoryDto
        {
            FixtureId = fixture.FixtureId,
            ApiFixtureId = fixture.ApiFixtureId,
            MarketName = normalizedMarketName,
            LatestCollectedAtUtc = snapshots.Max(x => x.CollectedAt),
            Series = series
        };
    }

    public async Task<IReadOnlyList<OddsMovementDto>> GetMovementAsync(
        long apiFixtureId,
        string? marketName = null,
        CancellationToken cancellationToken = default)
    {
        var fixture = await ResolveFixtureScopeAsync(apiFixtureId, cancellationToken);
        if (fixture is null)
            return Array.Empty<OddsMovementDto>();

        var normalizedMarketName = NormalizeMarketName(marketName);

        var rows = await (
                from openClose in _dbContext.OddsOpenCloses.AsNoTracking()
                join movement in _dbContext.OddsMovements.AsNoTracking()
                    on new { openClose.FixtureId, openClose.BookmakerId, openClose.MarketName }
                    equals new { movement.FixtureId, movement.BookmakerId, movement.MarketName }
                join bookmaker in _dbContext.Bookmakers.AsNoTracking()
                    on openClose.BookmakerId equals bookmaker.Id
                where openClose.FixtureId == fixture.FixtureId &&
                      openClose.MarketName == normalizedMarketName
                orderby bookmaker.Name
                select new OddsMovementDto
                {
                    BookmakerId = bookmaker.Id,
                    ApiBookmakerId = bookmaker.ApiBookmakerId,
                    Bookmaker = bookmaker.Name,
                    MarketName = openClose.MarketName,
                    SnapshotCount = movement.SnapshotCount,
                    FirstCollectedAtUtc = movement.FirstCollectedAtUtc,
                    LastCollectedAtUtc = movement.LastCollectedAtUtc,
                    ClosingCollectedAtUtc = openClose.ClosingCollectedAtUtc,
                    OpeningHomeOdd = openClose.OpeningHomeOdd,
                    OpeningDrawOdd = openClose.OpeningDrawOdd,
                    OpeningAwayOdd = openClose.OpeningAwayOdd,
                    LatestHomeOdd = openClose.LatestHomeOdd,
                    LatestDrawOdd = openClose.LatestDrawOdd,
                    LatestAwayOdd = openClose.LatestAwayOdd,
                    PeakHomeOdd = openClose.PeakHomeOdd,
                    PeakDrawOdd = openClose.PeakDrawOdd,
                    PeakAwayOdd = openClose.PeakAwayOdd,
                    ClosingHomeOdd = openClose.ClosingHomeOdd,
                    ClosingDrawOdd = openClose.ClosingDrawOdd,
                    ClosingAwayOdd = openClose.ClosingAwayOdd,
                    HomeDelta = movement.HomeDelta,
                    DrawDelta = movement.DrawDelta,
                    AwayDelta = movement.AwayDelta,
                    HomeChangePercent = movement.HomeChangePercent,
                    DrawChangePercent = movement.DrawChangePercent,
                    AwayChangePercent = movement.AwayChangePercent,
                    HomeSwing = movement.HomeSwing,
                    DrawSwing = movement.DrawSwing,
                    AwaySwing = movement.AwaySwing
                })
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<OddsConsensusDto?> GetConsensusAsync(
        long apiFixtureId,
        string? marketName = null,
        CancellationToken cancellationToken = default)
    {
        var fixture = await ResolveFixtureScopeAsync(apiFixtureId, cancellationToken);
        if (fixture is null)
            return null;

        var normalizedMarketName = NormalizeMarketName(marketName);

        var consensus = await _dbContext.MarketConsensuses
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.FixtureId == fixture.FixtureId && x.MarketName == normalizedMarketName,
                cancellationToken);

        if (consensus is null)
            return null;

        var bookmakerIds = new[]
        {
            consensus.BestHomeBookmakerId,
            consensus.BestDrawBookmakerId,
            consensus.BestAwayBookmakerId
        }
        .Where(x => x.HasValue)
        .Select(x => x!.Value)
        .Distinct()
        .ToList();

        var bookmakerNames = await _dbContext.Bookmakers
            .AsNoTracking()
            .Where(x => bookmakerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return new OddsConsensusDto
        {
            FixtureId = fixture.FixtureId,
            ApiFixtureId = fixture.ApiFixtureId,
            MarketName = consensus.MarketName,
            SampleSize = consensus.SampleSize,
            OpeningHomeConsensusOdd = consensus.OpeningHomeConsensusOdd,
            OpeningDrawConsensusOdd = consensus.OpeningDrawConsensusOdd,
            OpeningAwayConsensusOdd = consensus.OpeningAwayConsensusOdd,
            LatestHomeConsensusOdd = consensus.LatestHomeConsensusOdd,
            LatestDrawConsensusOdd = consensus.LatestDrawConsensusOdd,
            LatestAwayConsensusOdd = consensus.LatestAwayConsensusOdd,
            BestHomeOdd = consensus.BestHomeOdd,
            BestHomeBookmaker = ResolveBookmakerName(consensus.BestHomeBookmakerId, bookmakerNames),
            BestDrawOdd = consensus.BestDrawOdd,
            BestDrawBookmaker = ResolveBookmakerName(consensus.BestDrawBookmakerId, bookmakerNames),
            BestAwayOdd = consensus.BestAwayOdd,
            BestAwayBookmaker = ResolveBookmakerName(consensus.BestAwayBookmakerId, bookmakerNames),
            MaxHomeSpread = consensus.MaxHomeSpread,
            MaxDrawSpread = consensus.MaxDrawSpread,
            MaxAwaySpread = consensus.MaxAwaySpread,
            UpdatedAtUtc = consensus.UpdatedAtUtc
        };
    }

    public async Task<OddsValueSignalsDto?> GetValueSignalsAsync(
        long apiFixtureId,
        string? marketName = null,
        CancellationToken cancellationToken = default)
    {
        var consensus = await GetConsensusAsync(apiFixtureId, marketName, cancellationToken);
        if (consensus is null)
            return null;

        var signals = new List<OddsValueSignalItemDto>
        {
            BuildSignal(
                "Home",
                consensus.OpeningHomeConsensusOdd,
                consensus.LatestHomeConsensusOdd,
                consensus.BestHomeOdd,
                consensus.BestHomeBookmaker,
                consensus.MaxHomeSpread),
            BuildSignal(
                "Draw",
                consensus.OpeningDrawConsensusOdd,
                consensus.LatestDrawConsensusOdd,
                consensus.BestDrawOdd,
                consensus.BestDrawBookmaker,
                consensus.MaxDrawSpread),
            BuildSignal(
                "Away",
                consensus.OpeningAwayConsensusOdd,
                consensus.LatestAwayConsensusOdd,
                consensus.BestAwayOdd,
                consensus.BestAwayBookmaker,
                consensus.MaxAwaySpread)
        };

        return new OddsValueSignalsDto
        {
            FixtureId = consensus.FixtureId,
            ApiFixtureId = consensus.ApiFixtureId,
            MarketName = consensus.MarketName,
            UpdatedAtUtc = consensus.UpdatedAtUtc,
            Signals = signals
        };
    }

    private async Task<OddsAnalyticsRebuildResultDto> RebuildForFixturesAsync(
        IReadOnlyList<FixtureAnalyticsScope> fixtures,
        string normalizedMarketName,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        if (fixtures.Count == 0)
        {
            return new OddsAnalyticsRebuildResultDto
            {
                MarketName = normalizedMarketName,
                ExecutedAtUtc = nowUtc
            };
        }

        var fixtureIds = fixtures.Select(x => x.FixtureId).ToList();
        var fixtureLookup = fixtures.ToDictionary(x => x.FixtureId);

        var snapshots = await _dbContext.PreMatchOdds
            .AsNoTracking()
            .Where(x => fixtureIds.Contains(x.FixtureId) && x.MarketName == normalizedMarketName)
            .OrderBy(x => x.FixtureId)
            .ThenBy(x => x.BookmakerId)
            .ThenBy(x => x.CollectedAt)
            .ToListAsync(cancellationToken);

        await _dbContext.OddsOpenCloses
            .Where(x => fixtureIds.Contains(x.FixtureId) && x.MarketName == normalizedMarketName)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.OddsMovements
            .Where(x => fixtureIds.Contains(x.FixtureId) && x.MarketName == normalizedMarketName)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.MarketConsensuses
            .Where(x => fixtureIds.Contains(x.FixtureId) && x.MarketName == normalizedMarketName)
            .ExecuteDeleteAsync(cancellationToken);

        var openCloseRows = new List<OddsOpenClose>();
        var movementRows = new List<OddsMovement>();

        foreach (var group in snapshots.GroupBy(x => new { x.FixtureId, x.BookmakerId, x.MarketName }))
        {
            var ordered = group.OrderBy(x => x.CollectedAt).ToList();
            var first = ordered.First();
            var last = ordered.Last();
            var fixture = fixtureLookup[group.Key.FixtureId];
            var hasClosed = fixture.KickoffAtUtc <= nowUtc ||
                            FixtureStatusMapper.GetStateBucket(fixture.Status) != FixtureStateBucket.Upcoming;

            var peakHome = SelectMaxOdd(ordered, x => x.HomeOdd);
            var peakDraw = SelectMaxOdd(ordered, x => x.DrawOdd);
            var peakAway = SelectMaxOdd(ordered, x => x.AwayOdd);
            var homeSwing = CalculateSwing(ordered.Select(x => x.HomeOdd));
            var drawSwing = CalculateSwing(ordered.Select(x => x.DrawOdd));
            var awaySwing = CalculateSwing(ordered.Select(x => x.AwayOdd));

            openCloseRows.Add(new OddsOpenClose
            {
                FixtureId = group.Key.FixtureId,
                BookmakerId = group.Key.BookmakerId,
                MarketName = group.Key.MarketName,
                SnapshotCount = ordered.Count,
                OpeningHomeOdd = first.HomeOdd,
                OpeningDrawOdd = first.DrawOdd,
                OpeningAwayOdd = first.AwayOdd,
                OpeningCollectedAtUtc = first.CollectedAt,
                LatestHomeOdd = last.HomeOdd,
                LatestDrawOdd = last.DrawOdd,
                LatestAwayOdd = last.AwayOdd,
                LatestCollectedAtUtc = last.CollectedAt,
                PeakHomeOdd = peakHome?.Odd,
                PeakDrawOdd = peakDraw?.Odd,
                PeakAwayOdd = peakAway?.Odd,
                PeakHomeCollectedAtUtc = peakHome?.CollectedAtUtc,
                PeakDrawCollectedAtUtc = peakDraw?.CollectedAtUtc,
                PeakAwayCollectedAtUtc = peakAway?.CollectedAtUtc,
                ClosingHomeOdd = hasClosed ? last.HomeOdd : null,
                ClosingDrawOdd = hasClosed ? last.DrawOdd : null,
                ClosingAwayOdd = hasClosed ? last.AwayOdd : null,
                ClosingCollectedAtUtc = hasClosed ? last.CollectedAt : null,
                UpdatedAtUtc = nowUtc
            });

            movementRows.Add(new OddsMovement
            {
                FixtureId = group.Key.FixtureId,
                BookmakerId = group.Key.BookmakerId,
                MarketName = group.Key.MarketName,
                SnapshotCount = ordered.Count,
                FirstCollectedAtUtc = first.CollectedAt,
                LastCollectedAtUtc = last.CollectedAt,
                HomeDelta = CalculateDelta(first.HomeOdd, last.HomeOdd),
                DrawDelta = CalculateDelta(first.DrawOdd, last.DrawOdd),
                AwayDelta = CalculateDelta(first.AwayOdd, last.AwayOdd),
                HomeChangePercent = CalculatePercentChange(first.HomeOdd, last.HomeOdd),
                DrawChangePercent = CalculatePercentChange(first.DrawOdd, last.DrawOdd),
                AwayChangePercent = CalculatePercentChange(first.AwayOdd, last.AwayOdd),
                HomeSwing = homeSwing,
                DrawSwing = drawSwing,
                AwaySwing = awaySwing,
                UpdatedAtUtc = nowUtc
            });
        }

        var consensusRows = openCloseRows
            .GroupBy(x => new { x.FixtureId, x.MarketName })
            .Select(group =>
            {
                var latestHomeValues = group.Where(x => x.LatestHomeOdd.HasValue).Select(x => x.LatestHomeOdd!.Value).ToList();
                var latestDrawValues = group.Where(x => x.LatestDrawOdd.HasValue).Select(x => x.LatestDrawOdd!.Value).ToList();
                var latestAwayValues = group.Where(x => x.LatestAwayOdd.HasValue).Select(x => x.LatestAwayOdd!.Value).ToList();

                var bestHome = group.Where(x => x.LatestHomeOdd.HasValue).OrderByDescending(x => x.LatestHomeOdd).FirstOrDefault();
                var bestDraw = group.Where(x => x.LatestDrawOdd.HasValue).OrderByDescending(x => x.LatestDrawOdd).FirstOrDefault();
                var bestAway = group.Where(x => x.LatestAwayOdd.HasValue).OrderByDescending(x => x.LatestAwayOdd).FirstOrDefault();

                return new MarketConsensus
                {
                    FixtureId = group.Key.FixtureId,
                    MarketName = group.Key.MarketName,
                    SampleSize = group.Count(),
                    OpeningHomeConsensusOdd = Average(group.Select(x => x.OpeningHomeOdd)),
                    OpeningDrawConsensusOdd = Average(group.Select(x => x.OpeningDrawOdd)),
                    OpeningAwayConsensusOdd = Average(group.Select(x => x.OpeningAwayOdd)),
                    LatestHomeConsensusOdd = Average(group.Select(x => x.LatestHomeOdd)),
                    LatestDrawConsensusOdd = Average(group.Select(x => x.LatestDrawOdd)),
                    LatestAwayConsensusOdd = Average(group.Select(x => x.LatestAwayOdd)),
                    BestHomeOdd = bestHome?.LatestHomeOdd,
                    BestDrawOdd = bestDraw?.LatestDrawOdd,
                    BestAwayOdd = bestAway?.LatestAwayOdd,
                    BestHomeBookmakerId = bestHome?.BookmakerId,
                    BestDrawBookmakerId = bestDraw?.BookmakerId,
                    BestAwayBookmakerId = bestAway?.BookmakerId,
                    MaxHomeSpread = CalculateSpread(latestHomeValues),
                    MaxDrawSpread = CalculateSpread(latestDrawValues),
                    MaxAwaySpread = CalculateSpread(latestAwayValues),
                    UpdatedAtUtc = nowUtc
                };
            })
            .ToList();

        if (openCloseRows.Count > 0)
            _dbContext.OddsOpenCloses.AddRange(openCloseRows);

        if (movementRows.Count > 0)
            _dbContext.OddsMovements.AddRange(movementRows);

        if (consensusRows.Count > 0)
            _dbContext.MarketConsensuses.AddRange(consensusRows);

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var leagueSeason in fixtures
                     .Select(x => new { x.LeagueApiId, x.Season })
                     .Distinct())
        {
            await _syncStateService.SetLastSyncedAtAsync(
                "odds_analytics",
                leagueSeason.LeagueApiId,
                leagueSeason.Season,
                nowUtc,
                cancellationToken);
        }

        return new OddsAnalyticsRebuildResultDto
        {
            MarketName = normalizedMarketName,
            FixturesProcessed = fixtures.Count,
            OpenCloseRowsUpserted = openCloseRows.Count,
            MovementRowsUpserted = movementRows.Count,
            ConsensusRowsUpserted = consensusRows.Count,
            ExecutedAtUtc = nowUtc
        };
    }

    private async Task<FixtureAnalyticsScope?> ResolveFixtureScopeAsync(
        long apiFixtureId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Fixtures
            .AsNoTracking()
            .Include(x => x.League)
            .Where(x => x.ApiFixtureId == apiFixtureId)
            .Select(x => new FixtureAnalyticsScope
            {
                FixtureId = x.Id,
                ApiFixtureId = x.ApiFixtureId,
                LeagueApiId = x.League.ApiLeagueId,
                Season = x.Season,
                KickoffAtUtc = x.KickoffAt,
                Status = x.Status
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static OddsValueSignalItemDto BuildSignal(
        string outcome,
        decimal? openingConsensusOdd,
        decimal? latestConsensusOdd,
        decimal? bestOdd,
        string? bestBookmaker,
        decimal? maxSpread)
    {
        var marketDelta = CalculateDelta(openingConsensusOdd, latestConsensusOdd);
        var valueEdge = CalculateDelta(latestConsensusOdd, bestOdd);

        return new OddsValueSignalItemDto
        {
            Outcome = outcome,
            OpeningConsensusOdd = openingConsensusOdd,
            LatestConsensusOdd = latestConsensusOdd,
            BestOdd = bestOdd,
            BestBookmaker = bestBookmaker,
            MarketDelta = marketDelta,
            MarketDeltaPercent = CalculatePercentChange(openingConsensusOdd, latestConsensusOdd),
            ValueEdge = valueEdge,
            ValueEdgePercent = CalculatePercentChange(latestConsensusOdd, bestOdd),
            MaxSpread = maxSpread,
            HasPositiveEdge = valueEdge.HasValue && valueEdge.Value > 0
        };
    }

    private static PeakOddSnapshot? SelectMaxOdd(
        IReadOnlyList<PreMatchOdd> snapshots,
        Func<PreMatchOdd, decimal?> selector)
    {
        var best = snapshots
            .Where(x => selector(x).HasValue)
            .OrderByDescending(x => selector(x))
            .ThenByDescending(x => x.CollectedAt)
            .FirstOrDefault();

        return best is null
            ? null
            : new PeakOddSnapshot
            {
                Odd = selector(best),
                CollectedAtUtc = best.CollectedAt
            };
    }

    private static decimal? Average(IEnumerable<decimal?> values)
    {
        var list = values.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        return list.Count == 0 ? null : Math.Round(list.Average(), 4);
    }

    private static decimal? CalculateDelta(decimal? opening, decimal? latest)
    {
        return opening.HasValue && latest.HasValue
            ? latest.Value - opening.Value
            : null;
    }

    private static decimal? CalculatePercentChange(decimal? opening, decimal? latest)
    {
        if (!opening.HasValue || !latest.HasValue || opening.Value == 0)
            return null;

        return Math.Round(((latest.Value - opening.Value) / opening.Value) * 100m, 4);
    }

    private static decimal? CalculateSwing(IEnumerable<decimal?> values)
    {
        var list = values.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        return list.Count == 0 ? null : list.Max() - list.Min();
    }

    private static decimal? CalculateSpread(IReadOnlyList<decimal> values)
    {
        return values.Count == 0 ? null : values.Max() - values.Min();
    }

    private static string? ResolveBookmakerName(long? bookmakerId, IReadOnlyDictionary<long, string> lookup)
    {
        return bookmakerId.HasValue && lookup.TryGetValue(bookmakerId.Value, out var name)
            ? name
            : null;
    }

    private static string NormalizeMarketName(string? marketName)
    {
        return string.IsNullOrWhiteSpace(marketName)
            ? PreMatchOddsService.DefaultMarketName
            : marketName.Trim();
    }

    private sealed class FixtureAnalyticsScope
    {
        public long FixtureId { get; set; }
        public long ApiFixtureId { get; set; }
        public long LeagueApiId { get; set; }
        public int Season { get; set; }
        public DateTime KickoffAtUtc { get; set; }
        public string? Status { get; set; }
    }

    private sealed class PeakOddSnapshot
    {
        public decimal? Odd { get; set; }
        public DateTime CollectedAtUtc { get; set; }
    }
}
