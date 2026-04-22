using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Entities;
using SmartBets.Enums;
using SmartBets.Hubs;
using SmartBets.Models.ApiFootball;

namespace SmartBets.Services;

public class LiveOddsService
{
    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _apiService;
    private readonly TheOddsLiveOddsService _theOddsLiveOddsService;
    private readonly IOptionsMonitor<CoreDataAutomationOptions> _automationOptionsMonitor;
    private readonly SyncStateService _syncStateService;
    private readonly IHubContext<LiveOddsHub> _hubContext;
    private readonly ILogger<LiveOddsService> _logger;

    public LiveOddsService(
        AppDbContext dbContext,
        FootballApiService apiService,
        TheOddsLiveOddsService theOddsLiveOddsService,
        IOptionsMonitor<CoreDataAutomationOptions> automationOptionsMonitor,
        SyncStateService syncStateService,
        IHubContext<LiveOddsHub> hubContext,
        ILogger<LiveOddsService> logger)
    {
        _dbContext = dbContext;
        _apiService = apiService;
        _theOddsLiveOddsService = theOddsLiveOddsService;
        _automationOptionsMonitor = automationOptionsMonitor;
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
        CancellationToken cancellationToken = default)
    {
        if (!fixtureId.HasValue && !leagueId.HasValue)
            throw new InvalidOperationException("Provide fixtureId or leagueId for live odds sync.");

        var allowAllLiveOddsMarkets = _automationOptionsMonitor.CurrentValue.AllowAllLiveOddsMarkets;
        var allowedMatchWinnerBetIds = allowAllLiveOddsMarkets || betId.HasValue
            ? Array.Empty<long>()
            : await GetStoredMatchWinnerLiveBetIdsAsync(cancellationToken);
        var providerBetId = !allowAllLiveOddsMarkets && !betId.HasValue && allowedMatchWinnerBetIds.Count == 1
            ? allowedMatchWinnerBetIds[0]
            : betId;

        var remoteRows = await _apiService.GetLiveOddsAsync(
            fixtureId,
            leagueId,
            providerBetId,
            bookmakerId: null,
            cancellationToken);

        var result = new LiveOddsSyncResultDto
        {
            FixtureApiId = fixtureId,
            LeagueApiId = leagueId,
            BetApiId = betId,
            ExecutedAtUtc = DateTime.UtcNow
        };

        if (remoteRows.Count == 0 && fixtureId.HasValue && !leagueId.HasValue)
        {
            var storedFixture = await ResolveStoredFixtureAsync(fixtureId: null, apiFixtureId: fixtureId.Value, cancellationToken);
            if (storedFixture is not null && storedFixture.LeagueApiId > 0)
            {
                result.UsedLeagueFallback = true;
                result.FallbackLeagueApiId = storedFixture.LeagueApiId;

                var leagueRows = await _apiService.GetLiveOddsAsync(
                    fixtureId: null,
                    leagueId: storedFixture.LeagueApiId,
                    providerBetId,
                    bookmakerId: null,
                    cancellationToken);

                remoteRows = leagueRows
                    .Where(x => x.Fixture.Id == fixtureId.Value)
                    .ToList();
            }
        }

        if (!allowAllLiveOddsMarkets && !betId.HasValue)
        {
            remoteRows = FilterRemoteRowsToMatchWinnerMarkets(remoteRows, allowedMatchWinnerBetIds);
        }

        result.ProviderFixturesReceived = remoteRows.Count;
        result.ProviderBookmakersReceived = remoteRows.Sum(GetProviderBookmakerCount);
        result.ProviderReturnedEmpty = remoteRows.Count == 0;
        result.ProviderFixtureApiIdsSample = remoteRows
            .Select(x => x.Fixture.Id)
            .Distinct()
            .Take(10)
            .ToList();

        if (remoteRows.Count == 0)
            return result;

        await EnsureLiveBetTypesForRemoteRowsAsync(remoteRows, result.ExecutedAtUtc, cancellationToken);

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

        result.LocalFixturesResolved = localFixtures.Count;

        var fixturesByApiId = localFixtures.ToDictionary(x => x.ApiFixtureId);
        var localFixtureIds = localFixtures.Select(x => x.FixtureId).ToList();

        var existingBookmakers = await _dbContext.Bookmakers.ToListAsync(cancellationToken);
        var directOddsBookmaker = ResolveDirectOddsBookmakerIdentity(existingBookmakers);
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
        var missingFixtureApiIds = new HashSet<long>();
        var resolvedBookmakers = new Dictionary<long, ResolvedBookmakerSyncTarget>();
        var collectedAtUtc = DateTime.UtcNow;

        foreach (var remoteFixture in remoteRows)
        {
            if (!fixturesByApiId.TryGetValue(remoteFixture.Fixture.Id, out var fixture))
            {
                result.FixturesMissingInDatabase++;
                if (missingFixtureApiIds.Count < 10)
                {
                    missingFixtureApiIds.Add(remoteFixture.Fixture.Id);
                }
                continue;
            }

            result.FixturesMatched++;
            touchedScopes.Add(BuildLeagueScopeKey(fixture.LeagueApiId, fixture.Season));

            foreach (var apiBookmaker in GetProviderBookmakers(remoteFixture, directOddsBookmaker))
            {
                if (!resolvedBookmakers.ContainsKey(apiBookmaker.Id))
                {
                    resolvedBookmakers[apiBookmaker.Id] = new ResolvedBookmakerSyncTarget(
                        apiBookmaker.Id,
                        apiBookmaker.Name.Trim(),
                        apiBookmaker.Id > 0 ? "real" : "synthetic");
                }

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

                        var normalizedOutcome = NormalizeNullable(
                            MatchOutcomeNormalizer.Canonicalize(
                                value.Value,
                                fixture.HomeTeamName,
                                fixture.AwayTeamName)) ?? string.Empty;
                        var normalizedLine = NormalizeNullable(value.Handicap);
                        var stopped = value.Suspended ?? apiBet.Stopped ?? apiBookmaker.Stopped ?? GetFixtureStopped(remoteFixture);
                        var blocked = apiBet.Blocked ?? apiBookmaker.Blocked ?? GetFixtureBlocked(remoteFixture);
                        var finished = apiBet.Finished ?? apiBookmaker.Finished ?? GetFixtureFinished(remoteFixture);

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
                            BetName = LiveMatchWinnerMarket.ToCanonicalBetName(apiBet.Name),
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

        if (!allowAllLiveOddsMarkets && !betId.HasValue && localFixtureIds.Count > 0)
        {
            await NormalizeAndDeleteNonMatchWinnerLiveOddsAsync(localFixtureIds, cancellationToken);
        }

        result.MissingFixtureApiIdsSample = missingFixtureApiIds.ToList();

        if (resolvedBookmakers.Count == 1)
        {
            var resolvedBookmaker = resolvedBookmakers.Values.First();
            result.BookmakerApiId = resolvedBookmaker.ApiBookmakerId;
            result.Bookmaker = resolvedBookmaker.Name;
            result.BookmakerIdentityType = resolvedBookmaker.IdentityType;
        }

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
        bool latestOnly = true,
        CancellationToken cancellationToken = default)
    {
        var fixture = await ResolveFixtureAsync(fixtureId, apiFixtureId, cancellationToken);
        if (fixture is null)
            return Array.Empty<LiveOddsMarketDto>();

        if (!betId.HasValue)
        {
            var providerRows = await _theOddsLiveOddsService.GetStoredLiveOddsAsync(
                fixtureId: fixture.FixtureId,
                apiFixtureId: null,
                latestOnly: latestOnly,
                cancellationToken: cancellationToken);

            if (providerRows.Count > 0)
                return providerRows;
        }

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
            .OrderByDescending(x => x.CollectedAtUtc)
            .ThenBy(x => x.Bookmaker.Name)
            .ThenBy(x => x.BetName)
            .ThenBy(x => x.OutcomeLabel)
            .Select(x => new LiveOddsReadRow
            {
                FixtureId = x.FixtureId,
                BookmakerId = x.BookmakerId,
                ApiBookmakerId = x.Bookmaker.ApiBookmakerId,
                Bookmaker = x.Bookmaker.Name,
                ApiBetId = x.ApiBetId,
                BetName = x.BetName,
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

        if (rows.Count == 0)
            return Array.Empty<LiveOddsMarketDto>();

        if (rows.Any(x => x.ApiBookmakerId > 0))
        {
            rows = rows
                .Where(x => x.ApiBookmakerId > 0)
                .ToList();
        }

        var grouped = rows
            .GroupBy(x => new { x.BookmakerId, x.ApiBookmakerId, x.Bookmaker, x.ApiBetId, x.BetName });

        var result = new List<LiveOddsMarketDto>();

        foreach (var group in grouped)
        {
            var lastSnapshotCollectedAtUtc = group.Max(x => x.CollectedAtUtc);
            var selectedRows = latestOnly
                ? SelectLatestMarketRows(group, fixture)
                : group.OrderByDescending(x => x.CollectedAtUtc).ToList();

            if (selectedRows.Count == 0)
                continue;

            var effectiveCollectedAtUtc = latestOnly && lastSyncedAtUtc.HasValue && lastSyncedAtUtc.Value > lastSnapshotCollectedAtUtc
                ? lastSyncedAtUtc.Value
                : lastSnapshotCollectedAtUtc;

            result.Add(new LiveOddsMarketDto
            {
                FixtureId = fixture.FixtureId,
                ApiFixtureId = fixture.ApiFixtureId,
                BookmakerId = group.Key.BookmakerId,
                ApiBookmakerId = group.Key.ApiBookmakerId,
                Bookmaker = group.Key.Bookmaker,
                BookmakerIdentityType = group.Key.ApiBookmakerId > 0 ? "real" : "synthetic",
                ApiBetId = group.Key.ApiBetId,
                BetName = group.Key.BetName,
                CollectedAtUtc = effectiveCollectedAtUtc,
                LastSnapshotCollectedAtUtc = lastSnapshotCollectedAtUtc,
                LastSyncedAtUtc = lastSyncedAtUtc,
                Values = selectedRows
                    .OrderByDescending(x => x.IsMain ?? false)
                    .ThenBy(x => GetOutcomeSortOrder(x.OutcomeLabel, fixture.HomeTeamName, fixture.AwayTeamName))
                    .ThenBy(x => MatchOutcomeNormalizer.Canonicalize(
                        x.OutcomeLabel,
                        fixture.HomeTeamName,
                        fixture.AwayTeamName))
                    .ThenBy(x => x.Line)
                    .Select(x => new LiveOddsValueDto
                    {
                        OutcomeLabel = MatchOutcomeNormalizer.Canonicalize(
                            x.OutcomeLabel,
                            fixture.HomeTeamName,
                            fixture.AwayTeamName),
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
        bool latestOnly = true,
        CancellationToken cancellationToken = default)
    {
        if (!betId.HasValue)
        {
            try
            {
                var providerRows = await _theOddsLiveOddsService.GetLiveOddsWithCatchUpAsync(
                    fixtureId,
                    apiFixtureId,
                    latestOnly,
                    cancellationToken);

                if (providerRows.Count > 0)
                    return providerRows;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "The Odds live odds read failed for fixtureId={FixtureId}, apiFixtureId={ApiFixtureId}. Falling back to API-Football live odds.",
                    fixtureId,
                    apiFixtureId);
            }
        }

        try
        {
            var result = await GetLiveOddsAsync(
                fixtureId,
                apiFixtureId,
                betId,
                latestOnly,
                cancellationToken);

            var shouldUpgradeSyntheticOnlyRows =
                result.Count > 0 &&
                result.All(x => x.ApiBookmakerId == SingleSourceLiveBookmakerIdentity.SyntheticApiBookmakerId) &&
                result.Any(x => !SingleSourceLiveBookmakerIdentity.IsSingleSourceName(x.Bookmaker));

            if (result.Count > 0 && !shouldUpgradeSyntheticOnlyRows)
                return result;

            var fixture = await ResolveStoredFixtureAsync(fixtureId, apiFixtureId, cancellationToken);
            if (fixture is null || !ShouldAttemptLiveOddsCatchUp(fixture))
                return result;

            try
            {
                await SyncLiveOddsAsync(
                    fixtureId: fixture.ApiFixtureId,
                    leagueId: null,
                    betId,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Live odds catch-up failed for fixture {ApiFixtureId}. Returning cached data only.",
                    fixture.ApiFixtureId);

                return result;
            }

            var refreshedResult = await GetLiveOddsAsync(
                fixtureId,
                apiFixtureId,
                betId,
                latestOnly,
                cancellationToken);

            return refreshedResult.Count > 0
                ? refreshedResult
                : result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Live odds read failed for fixtureId={FixtureId}, apiFixtureId={ApiFixtureId}. Returning no live odds.",
                fixtureId,
                apiFixtureId);

            return Array.Empty<LiveOddsMarketDto>();
        }
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

        var theOddsSummariesByApiFixtureId = (await _theOddsLiveOddsService.GetFixtureOddsSummariesAsync(
                distinctApiFixtureIds,
                cancellationToken))
            .ToDictionary(x => x.ApiFixtureId);

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

        liveRows = liveRows
            .GroupBy(x => x.FixtureId)
            .SelectMany(group => group.Any(x => x.ApiBookmakerId > 0)
                ? group.Where(x => x.ApiBookmakerId > 0)
                : group)
            .ToList();

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

        var preMatchLatestRows = preMatchRows
            .GroupBy(x => new { x.FixtureId, x.ApiBookmakerId })
            .Select(group =>
            {
                var ordered = group
                    .OrderByDescending(x => x.CollectedAtUtc)
                    .ThenBy(x => x.Bookmaker)
                    .ToList();

                return ordered.FirstOrDefault(IsCompletePreMatchSummaryRow) ?? ordered[0];
            })
            .ToList();

        var result = fixtures
            .OrderBy(x => x.ApiFixtureId)
            .Select(fixture => BuildFixtureOddsSummary(
                fixture,
                liveRows.Where(x => x.FixtureId == fixture.FixtureId).ToList(),
                preMatchLatestRows.Where(x => x.FixtureId == fixture.FixtureId).ToList()))
            .Select(summary => theOddsSummariesByApiFixtureId.TryGetValue(summary.ApiFixtureId, out var theOddsSummary)
                ? theOddsSummary
                : summary)
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

        var markets = await GetLiveOddsWithCatchUpAsync(
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
                LeagueApiId = x.League.ApiLeagueId,
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
            var latestBookmakerRows = bookmakerGroup
                .GroupBy(x => GetCanonicalOutcomeKey(
                    x.OutcomeLabel,
                    fixture.HomeTeamName,
                    fixture.AwayTeamName))
                .Select(group => group
                    .OrderByDescending(x => x.CollectedAtUtc)
                    .ThenBy(x => x.Bookmaker)
                    .First())
                .ToList();

            foreach (var row in latestBookmakerRows)
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

        return HasCompleteSummaryValue(summary)
            ? summary
            : null;
    }

    private static IReadOnlyList<LiveOddsReadRow> SelectLatestMarketRows(
        IEnumerable<LiveOddsReadRow> group,
        FixtureScope fixture)
    {
        return group
            .GroupBy(x => new
            {
                OutcomeLabel = MatchOutcomeNormalizer.Canonicalize(
                    x.OutcomeLabel,
                    fixture.HomeTeamName,
                    fixture.AwayTeamName),
                x.Line
            })
            .Select(rows => rows
                .OrderByDescending(x => x.CollectedAtUtc)
                .ThenByDescending(x => x.IsMain ?? false)
                .ThenBy(x => x.OutcomeLabel)
                .First())
            .ToList();
    }

    private static string GetCanonicalOutcomeKey(
        string? outcomeLabel,
        string homeTeamName,
        string awayTeamName)
    {
        return MatchOutcomeNormalizer.Canonicalize(outcomeLabel, homeTeamName, awayTeamName);
    }

    private static int GetOutcomeSortOrder(
        string? outcomeLabel,
        string homeTeamName,
        string awayTeamName)
    {
        if (IsHomeOutcome(outcomeLabel, homeTeamName))
            return 0;

        if (IsDrawOutcome(outcomeLabel))
            return 1;

        if (IsAwayOutcome(outcomeLabel, awayTeamName))
            return 2;

        return 3;
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

        return HasCompleteSummaryValue(summary)
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

    private static bool HasCompleteSummaryValue(FixtureLiveOddsSummaryDto summary)
    {
        return summary.BestHomeOdd.HasValue &&
               summary.BestDrawOdd.HasValue &&
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

        return dto.HomeOdd.HasValue && dto.DrawOdd.HasValue && dto.AwayOdd.HasValue;
    }

    private static bool IsHomeOutcome(string? outcomeLabel, string homeTeamName)
    {
        return MatchOutcomeNormalizer.IsHomeOutcome(outcomeLabel, homeTeamName);
    }

    private static bool IsDrawOutcome(string? outcomeLabel)
    {
        return MatchOutcomeNormalizer.IsDrawOutcome(outcomeLabel);
    }

    private static bool IsAwayOutcome(string? outcomeLabel, string awayTeamName)
    {
        return MatchOutcomeNormalizer.IsAwayOutcome(outcomeLabel, awayTeamName);
    }

    private async Task<IReadOnlyList<long>> GetStoredMatchWinnerLiveBetIdsAsync(CancellationToken cancellationToken)
    {
        return (await _dbContext.LiveBetTypes
            .AsNoTracking()
            .OrderBy(x => x.ApiBetId)
            .Select(x => new { x.ApiBetId, x.Name })
            .ToListAsync(cancellationToken))
            .Where(x => LiveMatchWinnerMarket.IsMatchWinnerBetName(x.Name))
            .Select(x => x.ApiBetId)
            .ToList();
    }

    private static List<ApiFootballLiveOddsFixtureItem> FilterRemoteRowsToMatchWinnerMarkets(
        IReadOnlyList<ApiFootballLiveOddsFixtureItem> remoteRows,
        IReadOnlyCollection<long> allowedMatchWinnerBetIds)
    {
        var result = new List<ApiFootballLiveOddsFixtureItem>();

        foreach (var remoteFixture in remoteRows)
        {
            var filteredBookmakers = remoteFixture.Bookmakers
                .Select(bookmaker => new ApiFootballLiveOddsBookmaker
                {
                    Id = bookmaker.Id,
                    Name = bookmaker.Name,
                    Stopped = bookmaker.Stopped,
                    Blocked = bookmaker.Blocked,
                    Finished = bookmaker.Finished,
                    Bets = bookmaker.Bets
                        .Where(apiBet => ShouldKeepMatchWinnerBet(apiBet, allowedMatchWinnerBetIds))
                        .ToList()
                })
                .Where(x => x.Bets.Count > 0)
                .ToList();

            var filteredOdds = remoteFixture.Odds
                .Where(apiBet => ShouldKeepMatchWinnerBet(apiBet, allowedMatchWinnerBetIds))
                .ToList();

            if (filteredBookmakers.Count == 0 && filteredOdds.Count == 0)
                continue;

            result.Add(new ApiFootballLiveOddsFixtureItem
            {
                Fixture = remoteFixture.Fixture,
                League = remoteFixture.League,
                Bookmakers = filteredBookmakers,
                Odds = filteredOdds,
                Update = remoteFixture.Update,
                Status = remoteFixture.Status,
                Stopped = remoteFixture.Stopped,
                Blocked = remoteFixture.Blocked,
                Finished = remoteFixture.Finished
            });
        }

        return result;
    }

    private static bool ShouldKeepMatchWinnerBet(
        ApiFootballLiveOddsBet apiBet,
        IReadOnlyCollection<long> allowedMatchWinnerBetIds)
    {
        return allowedMatchWinnerBetIds.Contains(apiBet.Id) ||
               LiveMatchWinnerMarket.IsMatchWinnerBetName(apiBet.Name);
    }

    private async Task NormalizeAndDeleteNonMatchWinnerLiveOddsAsync(
        IReadOnlyCollection<long> fixtureIds,
        CancellationToken cancellationToken)
    {
        var distinctFixtureIds = fixtureIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        if (distinctFixtureIds.Count == 0)
            return;

        var aliasNames = LiveMatchWinnerMarket.GetUppercaseAliases();

        await _dbContext.LiveOdds
            .Where(x => distinctFixtureIds.Contains(x.FixtureId))
            .Where(x => aliasNames.Contains(x.BetName.ToUpper()) && x.BetName != PreMatchOddsService.DefaultMarketName)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.BetName, PreMatchOddsService.DefaultMarketName), cancellationToken);

        await _dbContext.LiveOdds
            .Where(x => distinctFixtureIds.Contains(x.FixtureId))
            .Where(x =>
                !aliasNames.Contains(x.BetName.ToUpper()) &&
                !EF.Functions.ILike(x.BetName, LiveMatchWinnerMarket.Contains1X2IlikePattern))
            .ExecuteDeleteAsync(cancellationToken);
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

    private async Task EnsureLiveBetTypesForRemoteRowsAsync(
        IReadOnlyCollection<ApiFootballLiveOddsFixtureItem> remoteRows,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        var providerBets = remoteRows
            .SelectMany(GetProviderBets)
            .Where(x => x.Id > 0)
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList();

        if (providerBets.Count == 0)
            return;

        var existing = await _dbContext.LiveBetTypes.ToListAsync(cancellationToken);
        var existingByApiId = existing.ToDictionary(x => x.ApiBetId);

        foreach (var providerBet in providerBets)
        {
            var normalizedName = string.IsNullOrWhiteSpace(providerBet.Name)
                ? $"Bet {providerBet.Id}"
                : providerBet.Name.Trim();

            if (!existingByApiId.TryGetValue(providerBet.Id, out var liveBetType))
            {
                liveBetType = new LiveBetType
                {
                    ApiBetId = providerBet.Id,
                    Name = normalizedName,
                    SyncedAtUtc = syncedAtUtc
                };

                _dbContext.LiveBetTypes.Add(liveBetType);
                existingByApiId[providerBet.Id] = liveBetType;
                continue;
            }

            if (!string.Equals(liveBetType.Name, normalizedName, StringComparison.Ordinal))
            {
                liveBetType.Name = normalizedName;
            }

            liveBetType.SyncedAtUtc = syncedAtUtc;
        }
    }

    private static IReadOnlyList<ProviderBookmakerScope> GetProviderBookmakers(
        ApiFootballLiveOddsFixtureItem fixture,
        ResolvedBookmakerIdentity directOddsBookmaker)
    {
        if (fixture.Bookmakers.Count > 0)
        {
            return fixture.Bookmakers
                .Select(x => new ProviderBookmakerScope(
                    x.Id,
                    x.Name,
                    x.Bets,
                    x.Stopped,
                    x.Blocked,
                    x.Finished))
                .ToList();
        }

        if (fixture.Odds.Count > 0)
        {
            return new[]
            {
                new ProviderBookmakerScope(
                    directOddsBookmaker.ApiBookmakerId,
                    directOddsBookmaker.Name,
                    fixture.Odds,
                    GetFixtureStopped(fixture),
                    GetFixtureBlocked(fixture),
                    GetFixtureFinished(fixture))
            };
        }

        return Array.Empty<ProviderBookmakerScope>();
    }

    private static ResolvedBookmakerIdentity ResolveDirectOddsBookmakerIdentity(
        IReadOnlyCollection<Bookmaker> existingBookmakers)
    {
        var bookmaker = existingBookmakers
            .Where(x => SingleSourceLiveBookmakerIdentity.IsSingleSourceName(x.Name))
            .OrderByDescending(x => x.ApiBookmakerId > 0)
            .ThenBy(x => x.ApiBookmakerId)
            .FirstOrDefault();

        if (bookmaker is not null)
        {
            return new ResolvedBookmakerIdentity(bookmaker.ApiBookmakerId, bookmaker.Name);
        }

        return new ResolvedBookmakerIdentity(
            SingleSourceLiveBookmakerIdentity.SyntheticApiBookmakerId,
            SingleSourceLiveBookmakerIdentity.Name);
    }

    private static IReadOnlyList<ApiFootballLiveOddsBet> GetProviderBets(ApiFootballLiveOddsFixtureItem fixture)
    {
        if (fixture.Bookmakers.Count > 0)
            return fixture.Bookmakers.SelectMany(x => x.Bets).ToList();

        return fixture.Odds;
    }

    private static int GetProviderBookmakerCount(ApiFootballLiveOddsFixtureItem fixture)
    {
        if (fixture.Bookmakers.Count > 0)
            return fixture.Bookmakers.Count;

        return fixture.Odds.Count > 0 ? 1 : 0;
    }

    private static int GetProviderBetCount(ApiFootballLiveOddsFixtureItem fixture)
    {
        if (fixture.Bookmakers.Count > 0)
            return fixture.Bookmakers.Sum(x => x.Bets.Count);

        return fixture.Odds.Count;
    }

    private static int GetProviderValueCount(ApiFootballLiveOddsFixtureItem fixture)
    {
        if (fixture.Bookmakers.Count > 0)
            return fixture.Bookmakers.Sum(x => x.Bets.Sum(y => y.Values.Count));

        return fixture.Odds.Sum(x => x.Values.Count);
    }

    private static bool? GetFixtureStopped(ApiFootballLiveOddsFixtureItem fixture)
    {
        return fixture.Status?.Stopped ?? fixture.Stopped;
    }

    private static bool? GetFixtureBlocked(ApiFootballLiveOddsFixtureItem fixture)
    {
        return fixture.Status?.Blocked ?? fixture.Blocked;
    }

    private static bool? GetFixtureFinished(ApiFootballLiveOddsFixtureItem fixture)
    {
        return fixture.Status?.Finished ?? fixture.Finished;
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
        public long LeagueApiId { get; set; }
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

    private sealed class LiveOddsReadRow
    {
        public long FixtureId { get; set; }
        public long BookmakerId { get; set; }
        public long ApiBookmakerId { get; set; }
        public string Bookmaker { get; set; } = string.Empty;
        public long ApiBetId { get; set; }
        public string BetName { get; set; } = string.Empty;
        public string OutcomeLabel { get; set; } = string.Empty;
        public string? Line { get; set; }
        public decimal? Odd { get; set; }
        public bool? IsMain { get; set; }
        public bool? Stopped { get; set; }
        public bool? Blocked { get; set; }
        public bool? Finished { get; set; }
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

    private static bool IsCompletePreMatchSummaryRow(PreMatchOddsSummaryRow row)
    {
        return row.HomeOdd.HasValue &&
               row.DrawOdd.HasValue &&
               row.AwayOdd.HasValue;
    }

    private sealed record LeagueSeasonScope(long LeagueApiId, int Season);

    private sealed record ProviderBookmakerScope(
        long Id,
        string Name,
        IReadOnlyList<ApiFootballLiveOddsBet> Bets,
        bool? Stopped,
        bool? Blocked,
        bool? Finished);

    private sealed record ResolvedBookmakerIdentity(long ApiBookmakerId, string Name);
    private sealed record ResolvedBookmakerSyncTarget(long ApiBookmakerId, string Name, string IdentityType);
}
