using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Entities;
using SmartBets.Enums;
using SmartBets.Hubs;

namespace SmartBets.Services;

public class LiveOddsService
{
    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _apiService;
    private readonly SyncStateService _syncStateService;
    private readonly IHubContext<LiveOddsHub> _hubContext;
    private readonly ILogger<LiveOddsService> _logger;

    public LiveOddsService(
        AppDbContext dbContext,
        FootballApiService apiService,
        SyncStateService syncStateService,
        IHubContext<LiveOddsHub> hubContext,
        ILogger<LiveOddsService> logger)
    {
        _dbContext = dbContext;
        _apiService = apiService;
        _syncStateService = syncStateService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<LiveBetTypesSyncResultDto> SyncLiveBetTypesAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var source = await _apiService.GetLiveOddsBetTypesAsync(cancellationToken);
        var existing = await _dbContext.LiveBetTypes.ToListAsync(cancellationToken);
        var existingByApiId = existing.ToDictionary(x => x.ApiBetId);

        var result = new LiveBetTypesSyncResultDto
        {
            ExecutedAtUtc = nowUtc
        };

        foreach (var item in source)
        {
            if (existingByApiId.TryGetValue(item.Id, out var row))
            {
                var changed = false;

                if (!string.Equals(row.Name, item.Name.Trim(), StringComparison.Ordinal))
                {
                    row.Name = item.Name.Trim();
                    changed = true;
                }

                row.SyncedAtUtc = nowUtc;

                if (changed)
                    result.Updated++;
            }
            else
            {
                _dbContext.LiveBetTypes.Add(new LiveBetType
                {
                    ApiBetId = item.Id,
                    Name = item.Name.Trim(),
                    SyncedAtUtc = nowUtc
                });

                result.Inserted++;
            }

            result.Processed++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _syncStateService.SetLastSyncedAtAsync("live_bet_types", null, null, nowUtc, cancellationToken);

        return result;
    }

    public async Task<IReadOnlyList<LiveBetTypeDto>> GetLiveBetTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.LiveBetTypes
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new LiveBetTypeDto
            {
                ApiBetId = x.ApiBetId,
                Name = x.Name,
                SyncedAtUtc = x.SyncedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<LiveOddsSyncResultDto> SyncLiveOddsAsync(
        long? fixtureId = null,
        long? leagueId = null,
        long? betId = null,
        long? bookmakerId = null,
        CancellationToken cancellationToken = default)
    {
        if (!fixtureId.HasValue && !leagueId.HasValue)
            throw new InvalidOperationException("Provide fixtureId or leagueId for live odds sync.");

        var remoteRows = await _apiService.GetLiveOddsAsync(
            fixtureId,
            leagueId,
            betId,
            bookmakerId,
            cancellationToken);

        var result = new LiveOddsSyncResultDto
        {
            FixtureApiId = fixtureId,
            LeagueApiId = leagueId,
            BetApiId = betId,
            BookmakerApiId = bookmakerId,
            ExecutedAtUtc = DateTime.UtcNow
        };

        if (remoteRows.Count == 0)
            return result;

        var apiFixtureIds = remoteRows.Select(x => x.Fixture.Id).Distinct().ToList();
        var summaryBeforeByApiFixtureId = (await GetFixtureOddsSummariesAsync(apiFixtureIds, cancellationToken))
            .ToDictionary(x => x.ApiFixtureId);
        var localFixtures = await _dbContext.Fixtures
            .AsNoTracking()
            .Include(x => x.League)
            .Where(x => apiFixtureIds.Contains(x.ApiFixtureId))
            .Select(x => new FixtureScope
            {
                FixtureId = x.Id,
                ApiFixtureId = x.ApiFixtureId,
                LeagueApiId = x.League.ApiLeagueId,
                Season = x.Season
            })
            .ToListAsync(cancellationToken);

        var fixturesByApiId = localFixtures.ToDictionary(x => x.ApiFixtureId);
        var localFixtureIds = localFixtures.Select(x => x.FixtureId).ToList();

        var existingBookmakers = await _dbContext.Bookmakers.ToListAsync(cancellationToken);
        var bookmakersByApiId = existingBookmakers.ToDictionary(x => x.ApiBookmakerId);

        var latestSnapshots = await _dbContext.LiveOdds
            .AsNoTracking()
            .Where(x => localFixtureIds.Contains(x.FixtureId))
            .Where(x => !betId.HasValue || x.ApiBetId == betId.Value)
            .OrderByDescending(x => x.CollectedAtUtc)
            .Select(x => new LiveOddSnapshotKey
            {
                FixtureId = x.FixtureId,
                ApiBookmakerId = x.Bookmaker.ApiBookmakerId,
                ApiBetId = x.ApiBetId,
                OutcomeLabel = x.OutcomeLabel,
                Line = x.Line,
                Odd = x.Odd,
                IsMain = x.IsMain,
                Stopped = x.Stopped,
                Blocked = x.Blocked,
                Finished = x.Finished,
                CollectedAtUtc = x.CollectedAtUtc
            })
            .ToListAsync(cancellationToken);

        var latestByKey = latestSnapshots
            .GroupBy(x => BuildSnapshotKey(x.FixtureId, x.ApiBookmakerId, x.ApiBetId, x.OutcomeLabel, x.Line))
            .ToDictionary(x => x.Key, x => x.First());

        var touchedScopes = new HashSet<string>(StringComparer.Ordinal);
        var changedFixturesByApiId = new Dictionary<long, FixtureScope>();
        var collectedAtUtc = DateTime.UtcNow;

        foreach (var remoteFixture in remoteRows)
        {
            if (!fixturesByApiId.TryGetValue(remoteFixture.Fixture.Id, out var fixture))
            {
                result.FixturesMissingInDatabase++;
                continue;
            }

            result.FixturesMatched++;
            touchedScopes.Add(BuildLeagueScopeKey(fixture.LeagueApiId, fixture.Season));

            foreach (var apiBookmaker in remoteFixture.Bookmakers)
            {
                result.BookmakersProcessed++;

                if (!bookmakersByApiId.TryGetValue(apiBookmaker.Id, out var bookmaker))
                {
                    bookmaker = new Bookmaker
                    {
                        ApiBookmakerId = apiBookmaker.Id,
                        Name = apiBookmaker.Name.Trim()
                    };

                    _dbContext.Bookmakers.Add(bookmaker);
                    bookmakersByApiId[apiBookmaker.Id] = bookmaker;
                    result.BookmakersInserted++;
                }
                else if (!string.Equals(bookmaker.Name, apiBookmaker.Name.Trim(), StringComparison.Ordinal))
                {
                    bookmaker.Name = apiBookmaker.Name.Trim();
                    result.BookmakersUpdated++;
                }

                foreach (var apiBet in apiBookmaker.Bets)
                {
                    result.BetsProcessed++;

                    foreach (var value in apiBet.Values)
                    {
                        if (!TryParseOdd(value.Odd, out var parsedOdd))
                            continue;

                        result.SnapshotsProcessed++;

                        var normalizedOutcome = NormalizeNullable(value.Value) ?? string.Empty;
                        var normalizedLine = NormalizeNullable(value.Handicap);
                        var stopped = apiBet.Stopped ?? apiBookmaker.Stopped ?? remoteFixture.Stopped;
                        var blocked = apiBet.Blocked ?? apiBookmaker.Blocked ?? remoteFixture.Blocked;
                        var finished = apiBet.Finished ?? apiBookmaker.Finished ?? remoteFixture.Finished;

                        var snapshotKey = BuildSnapshotKey(
                            fixture.FixtureId,
                            apiBookmaker.Id,
                            apiBet.Id,
                            normalizedOutcome,
                            normalizedLine);

                        if (latestByKey.TryGetValue(snapshotKey, out var latest) &&
                            latest.Odd == parsedOdd &&
                            latest.IsMain == value.Main &&
                            latest.Stopped == stopped &&
                            latest.Blocked == blocked &&
                            latest.Finished == finished)
                        {
                            result.SnapshotsSkippedUnchanged++;
                            continue;
                        }

                        _dbContext.LiveOdds.Add(new LiveOdd
                        {
                            FixtureId = fixture.FixtureId,
                            Bookmaker = bookmaker,
                            ApiBetId = apiBet.Id,
                            BetName = apiBet.Name.Trim(),
                            OutcomeLabel = normalizedOutcome,
                            Line = normalizedLine,
                            Odd = parsedOdd,
                            IsMain = value.Main,
                            Stopped = stopped,
                            Blocked = blocked,
                            Finished = finished,
                            CollectedAtUtc = collectedAtUtc
                        });

                        latestByKey[snapshotKey] = new LiveOddSnapshotKey
                        {
                            FixtureId = fixture.FixtureId,
                            ApiBookmakerId = apiBookmaker.Id,
                            ApiBetId = apiBet.Id,
                            OutcomeLabel = normalizedOutcome,
                            Line = normalizedLine,
                            Odd = parsedOdd,
                            IsMain = value.Main,
                            Stopped = stopped,
                            Blocked = blocked,
                            Finished = finished,
                            CollectedAtUtc = collectedAtUtc
                        };

                        changedFixturesByApiId[fixture.ApiFixtureId] = fixture;
                        result.SnapshotsInserted++;
                    }
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();

        var syncStateItems = touchedScopes
            .Select(ParseLeagueSeasonScope)
            .Where(x => x is not null)
            .Select(x => new SyncStateUpsertItem
            {
                EntityType = "live_odds",
                LeagueApiId = x!.LeagueApiId,
                Season = x.Season,
                SyncedAtUtc = collectedAtUtc
            })
            .ToList();

        await _syncStateService.SetLastSyncedAtBatchAsync(syncStateItems, cancellationToken);
        await BroadcastLiveOddsUpdatesAsync(changedFixturesByApiId.Values, cancellationToken);
        await BroadcastLiveOddsSummaryUpdatesAsync(
            changedFixturesByApiId.Keys.ToList(),
            summaryBeforeByApiFixtureId,
            cancellationToken);

        return result;
    }

    public async Task<IReadOnlyList<LiveOddsMarketDto>> GetLiveOddsAsync(
        long? fixtureId = null,
        long? apiFixtureId = null,
        long? betId = null,
        long? bookmakerId = null,
        bool latestOnly = true,
        CancellationToken cancellationToken = default)
    {
        var fixture = await ResolveFixtureAsync(fixtureId, apiFixtureId, cancellationToken);
        if (fixture is null)
            return Array.Empty<LiveOddsMarketDto>();

        var lastSyncedAtUtc = latestOnly
            ? await _dbContext.SyncStates
                .AsNoTracking()
                .Where(x =>
                    x.EntityType == "live_odds" &&
                    x.LeagueApiId == fixture.LeagueApiId &&
                    x.Season == fixture.Season)
                .Select(x => (DateTime?)x.LastSyncedAt)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var rows = await _dbContext.LiveOdds
            .AsNoTracking()
            .Where(x => x.FixtureId == fixture.FixtureId)
            .Where(x => !betId.HasValue || x.ApiBetId == betId.Value)
            .Where(x => !bookmakerId.HasValue || x.Bookmaker.ApiBookmakerId == bookmakerId.Value)
            .OrderByDescending(x => x.CollectedAtUtc)
            .ThenBy(x => x.Bookmaker.Name)
            .ThenBy(x => x.BetName)
            .ThenBy(x => x.OutcomeLabel)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return Array.Empty<LiveOddsMarketDto>();

        var grouped = rows
            .GroupBy(x => new { x.BookmakerId, x.Bookmaker.ApiBookmakerId, x.Bookmaker.Name, x.ApiBetId, x.BetName });

        var result = new List<LiveOddsMarketDto>();

        foreach (var group in grouped)
        {
            var selectedRows = latestOnly
                ? group.Where(x => x.CollectedAtUtc == group.Max(y => y.CollectedAtUtc)).ToList()
                : group.OrderByDescending(x => x.CollectedAtUtc).ToList();

            if (selectedRows.Count == 0)
                continue;

            var lastSnapshotCollectedAtUtc = selectedRows.Max(x => x.CollectedAtUtc);
            var effectiveCollectedAtUtc = latestOnly && lastSyncedAtUtc.HasValue && lastSyncedAtUtc.Value > lastSnapshotCollectedAtUtc
                ? lastSyncedAtUtc.Value
                : lastSnapshotCollectedAtUtc;

            result.Add(new LiveOddsMarketDto
            {
                FixtureId = fixture.FixtureId,
                ApiFixtureId = fixture.ApiFixtureId,
                BookmakerId = group.Key.BookmakerId,
                ApiBookmakerId = group.Key.ApiBookmakerId,
                Bookmaker = group.Key.Name,
                ApiBetId = group.Key.ApiBetId,
                BetName = group.Key.BetName,
                CollectedAtUtc = effectiveCollectedAtUtc,
                LastSnapshotCollectedAtUtc = lastSnapshotCollectedAtUtc,
                LastSyncedAtUtc = lastSyncedAtUtc,
                Values = selectedRows
                    .OrderByDescending(x => x.IsMain ?? false)
                    .ThenBy(x => x.OutcomeLabel)
                    .ThenBy(x => x.Line)
                    .Select(x => new LiveOddsValueDto
                    {
                        OutcomeLabel = x.OutcomeLabel,
                        Line = x.Line,
                        Odd = x.Odd,
                        IsMain = x.IsMain,
                        Stopped = x.Stopped,
                        Blocked = x.Blocked,
                        Finished = x.Finished
                    })
                    .ToList()
            });
        }

        return result
            .OrderBy(x => x.Bookmaker)
            .ThenBy(x => x.BetName)
            .ToList();
    }

    public async Task<IReadOnlyList<LiveOddsMarketDto>> GetLiveOddsWithCatchUpAsync(
        long? fixtureId = null,
        long? apiFixtureId = null,
        long? betId = null,
        long? bookmakerId = null,
        bool latestOnly = true,
        CancellationToken cancellationToken = default)
    {
        var result = await GetLiveOddsAsync(
            fixtureId,
            apiFixtureId,
            betId,
            bookmakerId,
            latestOnly,
            cancellationToken);

        if (result.Count > 0)
            return result;

        var fixture = await ResolveStoredFixtureAsync(fixtureId, apiFixtureId, cancellationToken);
        if (fixture is null || !ShouldAttemptLiveOddsCatchUp(fixture))
            return result;

        await SyncLiveOddsAsync(
            fixtureId: fixture.ApiFixtureId,
            leagueId: null,
            betId,
            bookmakerId,
            cancellationToken);

        return await GetLiveOddsAsync(
            fixtureId,
            apiFixtureId,
            betId,
            bookmakerId,
            latestOnly,
            cancellationToken);
    }

    public async Task<IReadOnlyList<FixtureLiveOddsSummaryDto>> GetFixtureOddsSummariesAsync(
        IReadOnlyCollection<long> apiFixtureIds,
        CancellationToken cancellationToken = default)
    {
        var distinctApiFixtureIds = apiFixtureIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        if (distinctApiFixtureIds.Count == 0)
            return Array.Empty<FixtureLiveOddsSummaryDto>();

        var fixtures = await _dbContext.Fixtures
            .AsNoTracking()
            .Where(x => distinctApiFixtureIds.Contains(x.ApiFixtureId))
            .Select(x => new FixtureSummaryContext
            {
                FixtureId = x.Id,
                ApiFixtureId = x.ApiFixtureId,
                LeagueApiId = x.League.ApiLeagueId,
                HomeTeamName = x.HomeTeam.Name,
                AwayTeamName = x.AwayTeam.Name
            })
            .ToListAsync(cancellationToken);

        if (fixtures.Count == 0)
            return Array.Empty<FixtureLiveOddsSummaryDto>();

        var fixtureIds = fixtures.Select(x => x.FixtureId).ToList();

        var liveRows = await _dbContext.LiveOdds
            .AsNoTracking()
            .Where(x => fixtureIds.Contains(x.FixtureId))
            .Where(x => x.BetName == PreMatchOddsService.DefaultMarketName)
            .Select(x => new LiveOddsSummaryRow
            {
                FixtureId = x.FixtureId,
                ApiBookmakerId = x.Bookmaker.ApiBookmakerId,
                Bookmaker = x.Bookmaker.Name,
                OutcomeLabel = x.OutcomeLabel,
                Odd = x.Odd,
                CollectedAtUtc = x.CollectedAtUtc
            })
            .ToListAsync(cancellationToken);

        var preMatchRows = await _dbContext.PreMatchOdds
            .AsNoTracking()
            .Where(x => fixtureIds.Contains(x.FixtureId))
            .Where(x => x.MarketName == PreMatchOddsService.DefaultMarketName)
            .Select(x => new PreMatchOddsSummaryRow
            {
                FixtureId = x.FixtureId,
                ApiBookmakerId = x.Bookmaker.ApiBookmakerId,
                Bookmaker = x.Bookmaker.Name,
                HomeOdd = x.HomeOdd,
                DrawOdd = x.DrawOdd,
                AwayOdd = x.AwayOdd,
                CollectedAtUtc = x.CollectedAt
            })
            .ToListAsync(cancellationToken);

        var liveLatestRows = liveRows
            .GroupBy(x => new { x.FixtureId, x.ApiBookmakerId })
            .SelectMany(group =>
            {
                var latestCollectedAtUtc = group.Max(x => x.CollectedAtUtc);
                return group.Where(x => x.CollectedAtUtc == latestCollectedAtUtc);
            })
            .ToList();

        var preMatchLatestRows = preMatchRows
            .GroupBy(x => new { x.FixtureId, x.ApiBookmakerId })
            .Select(group => group
                .OrderByDescending(x => x.CollectedAtUtc)
                .First())
            .ToList();

        var result = fixtures
            .OrderBy(x => x.ApiFixtureId)
            .Select(fixture => BuildFixtureOddsSummary(
                fixture,
                liveLatestRows.Where(x => x.FixtureId == fixture.FixtureId).ToList(),
                preMatchLatestRows.Where(x => x.FixtureId == fixture.FixtureId).ToList()))
            .ToList();

        return result;
    }

    public async Task<IReadOnlyList<OddDto>> GetMatchWinnerOddsAsync(
        long? fixtureId = null,
        long? apiFixtureId = null,
        bool latestOnly = true,
        CancellationToken cancellationToken = default)
    {
        var fixture = await ResolveFixtureAsync(fixtureId, apiFixtureId, cancellationToken);
        if (fixture is null)
            return Array.Empty<OddDto>();

        var markets = await GetLiveOddsWithCatchUpAsync(
            fixtureId,
            apiFixtureId,
            betId: null,
            bookmakerId: null,
            latestOnly: latestOnly,
            cancellationToken: cancellationToken);

        return markets
            .Where(x => string.Equals(x.BetName, PreMatchOddsService.DefaultMarketName, StringComparison.OrdinalIgnoreCase))
            .Select(x => TryMapMatchWinnerOdds(x, fixture, out var dto) ? dto : null)
            .Where(x => x is not null)
            .Cast<OddDto>()
            .OrderBy(x => x.Bookmaker)
            .ThenBy(x => x.MarketName)
            .ToList();
    }

    public async Task<BestOddsDto?> GetBestMatchWinnerOddsAsync(
        long? fixtureId = null,
        long? apiFixtureId = null,
        CancellationToken cancellationToken = default)
    {
        var odds = await GetMatchWinnerOddsAsync(
            fixtureId,
            apiFixtureId,
            latestOnly: true,
            cancellationToken: cancellationToken);

        if (odds.Count == 0)
            return null;

        var bestHome = odds
            .Where(x => x.HomeOdd.HasValue)
            .OrderByDescending(x => x.HomeOdd)
            .FirstOrDefault();

        var bestDraw = odds
            .Where(x => x.DrawOdd.HasValue)
            .OrderByDescending(x => x.DrawOdd)
            .FirstOrDefault();

        var bestAway = odds
            .Where(x => x.AwayOdd.HasValue)
            .OrderByDescending(x => x.AwayOdd)
            .FirstOrDefault();

        return new BestOddsDto
        {
            FixtureId = odds[0].FixtureId,
            ApiFixtureId = odds[0].ApiFixtureId,
            MarketName = PreMatchOddsService.DefaultMarketName,
            CollectedAtUtc = odds.Max(x => x.CollectedAtUtc),
            BestHomeOdd = bestHome?.HomeOdd,
            BestHomeBookmaker = bestHome?.Bookmaker,
            BestDrawOdd = bestDraw?.DrawOdd,
            BestDrawBookmaker = bestDraw?.Bookmaker,
            BestAwayOdd = bestAway?.AwayOdd,
            BestAwayBookmaker = bestAway?.Bookmaker
        };
    }

    public async Task<DateTime?> GetLatestMatchWinnerCollectedAtUtcAsync(
        long? fixtureId = null,
        long? apiFixtureId = null,
        CancellationToken cancellationToken = default)
    {
        var odds = await GetMatchWinnerOddsAsync(
            fixtureId,
            apiFixtureId,
            latestOnly: true,
            cancellationToken: cancellationToken);

        return odds.Count == 0
            ? null
            : odds.Max(x => x.CollectedAtUtc);
    }

    public async Task<LiveOddsUpdatedDto?> GetLiveOddsUpdateAsync(
        long apiFixtureId,
        CancellationToken cancellationToken = default)
    {
        var fixture = await ResolveFixtureAsync(
            fixtureId: null,
            apiFixtureId: apiFixtureId,
            cancellationToken);

        if (fixture is null)
            return null;

        var markets = await GetLiveOddsAsync(
            apiFixtureId: apiFixtureId,
            latestOnly: true,
            cancellationToken: cancellationToken);

        if (markets.Count == 0)
            return null;

        return new LiveOddsUpdatedDto
        {
            FixtureId = fixture.FixtureId,
            ApiFixtureId = fixture.ApiFixtureId,
            LeagueApiId = fixture.LeagueApiId,
            CollectedAtUtc = markets.Max(x => x.CollectedAtUtc),
            Markets = markets
        };
    }

    private async Task<FixtureScope?> ResolveFixtureAsync(
        long? fixtureId,
        long? apiFixtureId,
        CancellationToken cancellationToken)
    {
        if (!fixtureId.HasValue && !apiFixtureId.HasValue)
            return null;

        var query = _dbContext.Fixtures
            .AsNoTracking()
            .Include(x => x.League)
            .Select(x => new FixtureScope
            {
                FixtureId = x.Id,
                ApiFixtureId = x.ApiFixtureId,
                LeagueApiId = x.League.ApiLeagueId,
                Season = x.Season,
                HomeTeamName = x.HomeTeam.Name,
                AwayTeamName = x.AwayTeam.Name
            })
            .AsQueryable();

        if (fixtureId.HasValue)
            query = query.Where(x => x.FixtureId == fixtureId.Value);

        if (apiFixtureId.HasValue)
            query = query.Where(x => x.ApiFixtureId == apiFixtureId.Value);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<StoredFixtureScope?> ResolveStoredFixtureAsync(
        long? fixtureId,
        long? apiFixtureId,
        CancellationToken cancellationToken)
    {
        if (!fixtureId.HasValue && !apiFixtureId.HasValue)
            return null;

        var query = _dbContext.Fixtures
            .AsNoTracking()
            .Select(x => new StoredFixtureScope
            {
                FixtureId = x.Id,
                ApiFixtureId = x.ApiFixtureId,
                KickoffAt = x.KickoffAt,
                Status = x.Status
            })
            .AsQueryable();

        if (fixtureId.HasValue)
            query = query.Where(x => x.FixtureId == fixtureId.Value);

        if (apiFixtureId.HasValue)
            query = query.Where(x => x.ApiFixtureId == apiFixtureId.Value);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task BroadcastLiveOddsUpdatesAsync(
        IEnumerable<FixtureScope> changedFixtures,
        CancellationToken cancellationToken)
    {
        foreach (var fixture in changedFixtures.OrderBy(x => x.ApiFixtureId))
        {
            try
            {
                var payload = await GetLiveOddsUpdateAsync(fixture.ApiFixtureId, cancellationToken);
                if (payload is null)
                    continue;

                await _hubContext.Clients
                    .Group(LiveOddsHub.GetFixtureGroup(fixture.ApiFixtureId))
                    .SendAsync(LiveOddsHub.LiveOddsUpdatedEventName, payload, cancellationToken);

                await _hubContext.Clients
                    .Group(LiveOddsHub.GetLeagueGroup(fixture.LeagueApiId))
                    .SendAsync(LiveOddsHub.LiveOddsUpdatedEventName, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to broadcast live odds update for fixture {ApiFixtureId}. Live odds remain available via REST.",
                    fixture.ApiFixtureId);
            }
        }
    }

    private async Task BroadcastLiveOddsSummaryUpdatesAsync(
        IReadOnlyCollection<long> apiFixtureIds,
        IReadOnlyDictionary<long, FixtureLiveOddsSummaryDto> summariesBeforeByApiFixtureId,
        CancellationToken cancellationToken)
    {
        if (apiFixtureIds.Count == 0)
            return;

        var summariesAfter = await GetFixtureOddsSummariesAsync(apiFixtureIds, cancellationToken);
        foreach (var summary in summariesAfter)
        {
            summariesBeforeByApiFixtureId.TryGetValue(summary.ApiFixtureId, out var previousSummary);
            if (!HasSummaryChanged(previousSummary, summary))
                continue;

            try
            {
                await _hubContext.Clients
                    .Group(LiveOddsHub.GetFixtureGroup(summary.ApiFixtureId))
                    .SendAsync(LiveOddsHub.LiveOddsSummaryUpdatedEventName, summary, cancellationToken);

                await _hubContext.Clients
                    .Group(LiveOddsHub.GetLeagueGroup(summary.LeagueApiId))
                    .SendAsync(LiveOddsHub.LiveOddsSummaryUpdatedEventName, summary, cancellationToken);

                await _hubContext.Clients
                    .Group(LiveOddsHub.LiveFeedGroup)
                    .SendAsync(LiveOddsHub.LiveOddsSummaryUpdatedEventName, summary, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to broadcast live odds summary update for fixture {ApiFixtureId}.",
                    summary.ApiFixtureId);
            }
        }
    }

    private static bool ShouldAttemptLiveOddsCatchUp(StoredFixtureScope fixture)
    {
        var bucket = FixtureStatusMapper.GetStateBucket(fixture.Status);
        var nowUtc = DateTime.UtcNow;
        if (bucket is FixtureStateBucket.Postponed or FixtureStateBucket.Cancelled)
            return false;

        return fixture.KickoffAt >= nowUtc.AddHours(-4) &&
               fixture.KickoffAt <= nowUtc.AddMinutes(15);
    }

    private static FixtureLiveOddsSummaryDto BuildFixtureOddsSummary(
        FixtureSummaryContext fixture,
        IReadOnlyList<LiveOddsSummaryRow> liveRows,
        IReadOnlyList<PreMatchOddsSummaryRow> preMatchRows)
    {
        var liveSummary = BuildLiveSummary(fixture, liveRows);
        if (liveSummary is not null)
            return liveSummary;

        var preMatchSummary = BuildPreMatchSummary(fixture, preMatchRows);
        if (preMatchSummary is not null)
            return preMatchSummary;

        return new FixtureLiveOddsSummaryDto
        {
            ApiFixtureId = fixture.ApiFixtureId,
            LeagueApiId = fixture.LeagueApiId,
            Source = "none"
        };
    }

    private static FixtureLiveOddsSummaryDto? BuildLiveSummary(
        FixtureSummaryContext fixture,
        IReadOnlyList<LiveOddsSummaryRow> rows)
    {
        if (rows.Count == 0)
            return null;

        var summary = new FixtureLiveOddsSummaryDto
        {
            ApiFixtureId = fixture.ApiFixtureId,
            LeagueApiId = fixture.LeagueApiId,
            Source = "live",
            CollectedAtUtc = rows.Max(x => x.CollectedAtUtc)
        };

        foreach (var bookmakerGroup in rows.GroupBy(x => x.Bookmaker))
        {
            foreach (var row in bookmakerGroup)
            {
                if (IsHomeOutcome(row.OutcomeLabel, fixture.HomeTeamName))
                {
                    ApplyBestOdd(row.Odd, row.Bookmaker, summary.BestHomeOdd, summary.BestHomeBookmaker,
                        (odd, bookmaker) =>
                        {
                            summary.BestHomeOdd = odd;
                            summary.BestHomeBookmaker = bookmaker;
                        });
                }
                else if (IsDrawOutcome(row.OutcomeLabel))
                {
                    ApplyBestOdd(row.Odd, row.Bookmaker, summary.BestDrawOdd, summary.BestDrawBookmaker,
                        (odd, bookmaker) =>
                        {
                            summary.BestDrawOdd = odd;
                            summary.BestDrawBookmaker = bookmaker;
                        });
                }
                else if (IsAwayOutcome(row.OutcomeLabel, fixture.AwayTeamName))
                {
                    ApplyBestOdd(row.Odd, row.Bookmaker, summary.BestAwayOdd, summary.BestAwayBookmaker,
                        (odd, bookmaker) =>
                        {
                            summary.BestAwayOdd = odd;
                            summary.BestAwayBookmaker = bookmaker;
                        });
                }
            }
        }

        return HasAnySummaryValue(summary)
            ? summary
            : null;
    }

    private static FixtureLiveOddsSummaryDto? BuildPreMatchSummary(
        FixtureSummaryContext fixture,
        IReadOnlyList<PreMatchOddsSummaryRow> rows)
    {
        if (rows.Count == 0)
            return null;

        var summary = new FixtureLiveOddsSummaryDto
        {
            ApiFixtureId = fixture.ApiFixtureId,
            LeagueApiId = fixture.LeagueApiId,
            Source = "prematch",
            CollectedAtUtc = rows.Max(x => x.CollectedAtUtc)
        };

        foreach (var row in rows)
        {
            ApplyBestOdd(row.HomeOdd, row.Bookmaker, summary.BestHomeOdd, summary.BestHomeBookmaker,
                (odd, bookmaker) =>
                {
                    summary.BestHomeOdd = odd;
                    summary.BestHomeBookmaker = bookmaker;
                });
            ApplyBestOdd(row.DrawOdd, row.Bookmaker, summary.BestDrawOdd, summary.BestDrawBookmaker,
                (odd, bookmaker) =>
                {
                    summary.BestDrawOdd = odd;
                    summary.BestDrawBookmaker = bookmaker;
                });
            ApplyBestOdd(row.AwayOdd, row.Bookmaker, summary.BestAwayOdd, summary.BestAwayBookmaker,
                (odd, bookmaker) =>
                {
                    summary.BestAwayOdd = odd;
                    summary.BestAwayBookmaker = bookmaker;
                });
        }

        return HasAnySummaryValue(summary)
            ? summary
            : null;
    }

    private static void ApplyBestOdd(
        decimal? candidateOdd,
        string bookmaker,
        decimal? currentOdd,
        string? currentBookmaker,
        Action<decimal?, string?> apply)
    {
        if (!candidateOdd.HasValue)
            return;

        if (!currentOdd.HasValue || candidateOdd > currentOdd)
        {
            apply(candidateOdd, bookmaker);
            return;
        }

        if (candidateOdd == currentOdd &&
            string.Compare(bookmaker, currentBookmaker, StringComparison.OrdinalIgnoreCase) < 0)
        {
            apply(candidateOdd, bookmaker);
        }
    }

    private static bool HasAnySummaryValue(FixtureLiveOddsSummaryDto summary)
    {
        return summary.BestHomeOdd.HasValue ||
               summary.BestDrawOdd.HasValue ||
               summary.BestAwayOdd.HasValue;
    }

    private static bool HasSummaryChanged(FixtureLiveOddsSummaryDto? before, FixtureLiveOddsSummaryDto after)
    {
        if (before is null)
            return true;

        return before.Source != after.Source ||
               before.CollectedAtUtc != after.CollectedAtUtc ||
               before.BestHomeOdd != after.BestHomeOdd ||
               !string.Equals(before.BestHomeBookmaker, after.BestHomeBookmaker, StringComparison.Ordinal) ||
               before.BestDrawOdd != after.BestDrawOdd ||
               !string.Equals(before.BestDrawBookmaker, after.BestDrawBookmaker, StringComparison.Ordinal) ||
               before.BestAwayOdd != after.BestAwayOdd ||
               !string.Equals(before.BestAwayBookmaker, after.BestAwayBookmaker, StringComparison.Ordinal);
    }

    private static bool TryMapMatchWinnerOdds(
        LiveOddsMarketDto market,
        FixtureScope fixture,
        out OddDto dto)
    {
        dto = new OddDto
        {
            FixtureId = market.FixtureId,
            ApiFixtureId = market.ApiFixtureId,
            BookmakerId = market.BookmakerId,
            ApiBookmakerId = market.ApiBookmakerId,
            Bookmaker = market.Bookmaker,
            MarketName = PreMatchOddsService.DefaultMarketName,
            CollectedAtUtc = market.CollectedAtUtc
        };

        foreach (var value in market.Values)
        {
            if (IsHomeOutcome(value.OutcomeLabel, fixture.HomeTeamName))
            {
                dto.HomeOdd = value.Odd;
            }
            else if (IsDrawOutcome(value.OutcomeLabel))
            {
                dto.DrawOdd = value.Odd;
            }
            else if (IsAwayOutcome(value.OutcomeLabel, fixture.AwayTeamName))
            {
                dto.AwayOdd = value.Odd;
            }
        }

        return dto.HomeOdd.HasValue || dto.DrawOdd.HasValue || dto.AwayOdd.HasValue;
    }

    private static bool IsHomeOutcome(string? outcomeLabel, string homeTeamName)
    {
        if (string.IsNullOrWhiteSpace(outcomeLabel))
            return false;

        var normalized = NormalizeOutcome(outcomeLabel);
        var normalizedHomeTeam = NormalizeOutcome(homeTeamName);

        return normalized is "1" or "HOME" ||
               normalized == normalizedHomeTeam;
    }

    private static bool IsDrawOutcome(string? outcomeLabel)
    {
        if (string.IsNullOrWhiteSpace(outcomeLabel))
            return false;

        var normalized = NormalizeOutcome(outcomeLabel);
        return normalized is "X" or "DRAW" or "TIE";
    }

    private static bool IsAwayOutcome(string? outcomeLabel, string awayTeamName)
    {
        if (string.IsNullOrWhiteSpace(outcomeLabel))
            return false;

        var normalized = NormalizeOutcome(outcomeLabel);
        var normalizedAwayTeam = NormalizeOutcome(awayTeamName);

        return normalized is "2" or "AWAY" ||
               normalized == normalizedAwayTeam;
    }

    private static string NormalizeOutcome(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static string BuildSnapshotKey(
        long fixtureId,
        long apiBookmakerId,
        long apiBetId,
        string outcomeLabel,
        string? line)
    {
        return $"{fixtureId}:{apiBookmakerId}:{apiBetId}:{outcomeLabel}:{line ?? string.Empty}".ToUpperInvariant();
    }

    private static string BuildLeagueScopeKey(long leagueApiId, int season)
    {
        return $"{leagueApiId}:{season}";
    }

    private static LeagueSeasonScope? ParseLeagueSeasonScope(string scope)
    {
        var parts = scope.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return null;

        if (!long.TryParse(parts[0], out var leagueApiId) || !int.TryParse(parts[1], out var season))
            return null;

        return new LeagueSeasonScope(leagueApiId, season);
    }

    private static bool TryParseOdd(string? value, out decimal odd)
    {
        return decimal.TryParse(
            value,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out odd);
    }

    private static string? NormalizeNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private sealed class FixtureScope
    {
        public long FixtureId { get; set; }
        public long ApiFixtureId { get; set; }
        public long LeagueApiId { get; set; }
        public int Season { get; set; }
        public string HomeTeamName { get; set; } = string.Empty;
        public string AwayTeamName { get; set; } = string.Empty;
    }

    private sealed class StoredFixtureScope
    {
        public long FixtureId { get; set; }
        public long ApiFixtureId { get; set; }
        public DateTime KickoffAt { get; set; }
        public string? Status { get; set; }
    }

    private sealed class FixtureSummaryContext
    {
        public long FixtureId { get; set; }
        public long ApiFixtureId { get; set; }
        public long LeagueApiId { get; set; }
        public string HomeTeamName { get; set; } = string.Empty;
        public string AwayTeamName { get; set; } = string.Empty;
    }

    private sealed class LiveOddSnapshotKey
    {
        public long FixtureId { get; set; }
        public long ApiBookmakerId { get; set; }
        public long ApiBetId { get; set; }
        public string OutcomeLabel { get; set; } = string.Empty;
        public string? Line { get; set; }
        public decimal? Odd { get; set; }
        public bool? IsMain { get; set; }
        public bool? Stopped { get; set; }
        public bool? Blocked { get; set; }
        public bool? Finished { get; set; }
        public DateTime CollectedAtUtc { get; set; }
    }

    private sealed class LiveOddsSummaryRow
    {
        public long FixtureId { get; set; }
        public long ApiBookmakerId { get; set; }
        public string Bookmaker { get; set; } = string.Empty;
        public string OutcomeLabel { get; set; } = string.Empty;
        public decimal? Odd { get; set; }
        public DateTime CollectedAtUtc { get; set; }
    }

    private sealed class PreMatchOddsSummaryRow
    {
        public long FixtureId { get; set; }
        public long ApiBookmakerId { get; set; }
        public string Bookmaker { get; set; } = string.Empty;
        public decimal? HomeOdd { get; set; }
        public decimal? DrawOdd { get; set; }
        public decimal? AwayOdd { get; set; }
        public DateTime CollectedAtUtc { get; set; }
    }

    private sealed record LeagueSeasonScope(long LeagueApiId, int Season);
}
