using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Entities;
using SmartBets.Enums;
using SmartBets.Models.TheOddsApi;

namespace SmartBets.Services;

public class TheOddsLiveOddsService
{
    private static readonly HashSet<string> LiveStatuses = FixtureStatusMapper
        .GetStatusesForBucket(FixtureStateBucket.Live)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> TeamNameAliases = new(StringComparer.Ordinal)
    {
        ["ARSENAL"] = "ARSENAL",
        ["ASTONVILLA"] = "ASTONVILLA",
        ["ATHLETICCLUB"] = "ATHLETICBILBAO",
        ["ATHLETICBILBAO"] = "ATHLETICBILBAO",
        ["ATLETICODEMADRID"] = "ATLETICOMADRID",
        ["ATLETICOMADRID"] = "ATLETICOMADRID",
        ["BAYERNMUNICH"] = "BAYERNMUNICH",
        ["BORUSSIADORTMUND"] = "BORUSSIADORTMUND",
        ["BRIGHTON"] = "BRIGHTONHOVEALBION",
        ["BRIGHTONHOVEALBION"] = "BRIGHTONHOVEALBION",
        ["CHELSEA"] = "CHELSEA",
        ["CRYSTALPALACE"] = "CRYSTALPALACE",
        ["EVERTON"] = "EVERTON",
        ["INTER"] = "INTERMILAN",
        ["INTERMILAN"] = "INTERMILAN",
        ["INTERNAZIONALE"] = "INTERMILAN",
        ["JUVENTUS"] = "JUVENTUS",
        ["LEICESTER"] = "LEICESTERCITY",
        ["LEICESTERCITY"] = "LEICESTERCITY",
        ["LIVERPOOL"] = "LIVERPOOL",
        ["MANCHESTERCITY"] = "MANCHESTERCITY",
        ["MANCITY"] = "MANCHESTERCITY",
        ["MANCHESTERUNITED"] = "MANCHESTERUNITED",
        ["MANUNITED"] = "MANCHESTERUNITED",
        ["MANUTD"] = "MANCHESTERUNITED",
        ["NEWCASTLE"] = "NEWCASTLEUNITED",
        ["NEWCASTLEUNITED"] = "NEWCASTLEUNITED",
        ["NOTTINGHAMFOREST"] = "NOTTINGHAMFOREST",
        ["NOTTMFOREST"] = "NOTTINGHAMFOREST",
        ["PARISSAINTGERMAIN"] = "PARISSAINTGERMAIN",
        ["PSG"] = "PARISSAINTGERMAIN",
        ["ROMA"] = "ASROMA",
        ["ASROMA"] = "ASROMA",
        ["SHEFFIELDUNITED"] = "SHEFFIELDUNITED",
        ["SHEFFUTD"] = "SHEFFIELDUNITED",
        ["SPURS"] = "TOTTENHAMHOTSPUR",
        ["TOTTENHAM"] = "TOTTENHAMHOTSPUR",
        ["TOTTENHAMHOTSPUR"] = "TOTTENHAMHOTSPUR",
        ["WESTHAM"] = "WESTHAMUNITED",
        ["WESTHAMUNITED"] = "WESTHAMUNITED",
        ["WOLVERHAMPTON"] = "WOLVERHAMPTONWANDERERS",
        ["WOLVERHAMPTONWANDERERS"] = "WOLVERHAMPTONWANDERERS",
        ["WOLVES"] = "WOLVERHAMPTONWANDERERS"
    };

    private readonly AppDbContext _dbContext;
    private readonly TheOddsApiService _apiService;
    private readonly TheOddsSportKeyResolverService _sportKeyResolver;
    private readonly SyncStateService _syncStateService;
    private readonly IOptionsMonitor<TheOddsApiOptions> _optionsMonitor;
    private readonly ILogger<TheOddsLiveOddsService> _logger;

    public TheOddsLiveOddsService(
        AppDbContext dbContext,
        TheOddsApiService apiService,
        TheOddsSportKeyResolverService sportKeyResolver,
        SyncStateService syncStateService,
        IOptionsMonitor<TheOddsApiOptions> optionsMonitor,
        ILogger<TheOddsLiveOddsService> logger)
    {
        _dbContext = dbContext;
        _apiService = apiService;
        _sportKeyResolver = sportKeyResolver;
        _syncStateService = syncStateService;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<TheOddsLiveOddsSyncResultDto> SyncLeagueLiveOddsAsync(
        long leagueApiId,
        int season,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        var result = new TheOddsLiveOddsSyncResultDto
        {
            LeagueApiId = leagueApiId,
            Season = season,
            ProviderEnabled = options.Enabled,
            ProviderConfigured = options.IsConfigured(),
            Regions = options.GetRegions(),
            MarketKey = options.GetMarketKey(),
            ExecutedAtUtc = DateTime.UtcNow
        };

        if (!options.Enabled)
        {
            result.SkippedReason = "provider_disabled";
            return result;
        }

        if (!options.IsConfigured())
        {
            result.SkippedReason = "provider_not_configured";
            return result;
        }

        try
        {
            var fixtures = await _dbContext.Fixtures
                .AsNoTracking()
                .Where(x =>
                    x.League.ApiLeagueId == leagueApiId &&
                    x.Season == season &&
                    x.Status != null &&
                    LiveStatuses.Contains(x.Status))
                .Select(x => new FixtureScope
                {
                    FixtureId = x.Id,
                    ApiFixtureId = x.ApiFixtureId,
                    LeagueApiId = x.League.ApiLeagueId,
                    Season = x.Season,
                    KickoffAtUtc = x.KickoffAt,
                    Status = x.Status,
                    HomeTeamName = x.HomeTeam.Name,
                    AwayTeamName = x.AwayTeam.Name
                })
                .ToListAsync(cancellationToken);

            if (fixtures.Count == 0)
            {
                result.SkippedReason = "no_live_fixtures_in_scope";
                return result;
            }

            if (!force &&
                await HasRecentLeagueSyncAsync(leagueApiId, season, options.GetMinLeagueSyncInterval(), cancellationToken))
            {
                result.SkippedReason = "recently_synced";
                return result;
            }

            options.TryGetSportKey(leagueApiId, out var configuredSportKey);
            var resolution = await _sportKeyResolver.ResolveAsync(
                leagueApiId,
                season,
                configuredSportKey,
                fixtures.Select(x => new TheOddsFixtureLookupContext
                {
                    KickoffAtUtc = x.KickoffAtUtc,
                    HomeTeamName = x.HomeTeamName,
                    AwayTeamName = x.AwayTeamName
                }).ToList(),
                options.GetMatchTolerance(),
                cancellationToken);

            result.RequestsUsed += resolution.RequestsUsed;

            if (string.IsNullOrWhiteSpace(resolution.SportKey))
            {
                result.SkippedReason = "unable_to_resolve_league_sport_key";
                result.SportKeySource = resolution.Source;
                result.SportKeyConfidence = resolution.Confidence;
                result.SportKeyVerified = resolution.IsVerified;
                return result;
            }

            var sportKey = resolution.SportKey!;
            result.SportKey = sportKey;
            result.SportKeySource = resolution.Source;
            result.SportKeyConfidence = resolution.Confidence;
            result.SportKeyVerified = resolution.IsVerified;

            var syncResult = await SyncFixtureScopeGroupAsync(fixtures, sportKey, options, cancellationToken);
            result.RequestsUsed += syncResult.RequestsUsed;
            result.ProviderEventsReceived = syncResult.ProviderEventsReceived;
            result.FixturesMatched = syncResult.FixturesMatched;
            result.FixturesMissingMatch = syncResult.FixturesMissingMatch;
            result.BookmakersProcessed = syncResult.BookmakersProcessed;
            result.SnapshotsProcessed = syncResult.SnapshotsProcessed;
            result.SnapshotsInserted = syncResult.SnapshotsInserted;
            result.SnapshotsSkippedUnchanged = syncResult.SnapshotsSkippedUnchanged;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "The Odds live sync failed for league {LeagueApiId}/{Season}.",
                leagueApiId,
                season);

            result.SkippedReason = "provider_sync_failed";
            result.ProviderError = BuildSafeErrorMessage(ex);
            return result;
        }
    }

    public async Task<TheOddsLiveOddsBatchSyncResult> SyncFixturesLiveOddsAsync(
        IReadOnlyCollection<long> apiFixtureIds,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        var result = new TheOddsLiveOddsBatchSyncResult
        {
            ProviderEnabled = options.Enabled,
            ProviderConfigured = options.IsConfigured(),
            ExecutedAtUtc = DateTime.UtcNow
        };

        if (!options.Enabled)
        {
            result.SkippedReason = "provider_disabled";
            return result;
        }

        if (!options.IsConfigured())
        {
            result.SkippedReason = "provider_not_configured";
            return result;
        }

        var distinctApiFixtureIds = apiFixtureIds
            .Where(x => x > 0)
            .Distinct()
            .Take(options.GetMaxViewerFixturesPerCycle())
            .ToList();

        if (distinctApiFixtureIds.Count == 0)
        {
            result.SkippedReason = "no_fixtures_requested";
            return result;
        }

        try
        {
            var fixtures = await _dbContext.Fixtures
                .AsNoTracking()
                .Where(x => distinctApiFixtureIds.Contains(x.ApiFixtureId))
                .Where(x => x.Status != null && LiveStatuses.Contains(x.Status))
                .Select(x => new FixtureScope
                {
                    FixtureId = x.Id,
                    ApiFixtureId = x.ApiFixtureId,
                    LeagueApiId = x.League.ApiLeagueId,
                    Season = x.Season,
                    KickoffAtUtc = x.KickoffAt,
                    Status = x.Status,
                    HomeTeamName = x.HomeTeam.Name,
                    AwayTeamName = x.AwayTeam.Name
                })
                .ToListAsync(cancellationToken);

            result.FixturesRequested = distinctApiFixtureIds.Count;
            result.LiveFixturesResolved = fixtures.Count;

            if (fixtures.Count == 0)
            {
                result.SkippedReason = "no_live_fixtures_in_scope";
                return result;
            }

            var groups = fixtures
                .GroupBy(x => x.LeagueApiId)
                .Select(group => new
                {
                    LeagueApiId = group.Key,
                    Season = group.Select(x => x.Season).First(),
                    Fixtures = group.ToList()
                })
                .ToList();

            result.LeaguesRequested = groups.Count;

            foreach (var group in groups)
            {
                if (!force &&
                    await HasRecentLeagueSyncAsync(
                        group.LeagueApiId,
                        group.Season,
                        options.GetMinLeagueSyncInterval(),
                        cancellationToken))
                {
                    result.LeaguesSkippedRecentlySynced++;
                    continue;
                }

                options.TryGetSportKey(group.LeagueApiId, out var configuredSportKey);
                var resolution = await _sportKeyResolver.ResolveAsync(
                    group.LeagueApiId,
                    group.Season,
                    configuredSportKey,
                    group.Fixtures.Select(x => new TheOddsFixtureLookupContext
                    {
                        KickoffAtUtc = x.KickoffAtUtc,
                        HomeTeamName = x.HomeTeamName,
                        AwayTeamName = x.AwayTeamName
                    }).ToList(),
                    options.GetMatchTolerance(),
                    cancellationToken);

                result.RequestsUsed += resolution.RequestsUsed;

                if (string.IsNullOrWhiteSpace(resolution.SportKey))
                {
                    result.LeaguesMissingSportKeyMapping++;
                    result.LeaguesUnresolvedSportKey++;
                    continue;
                }

                var sportKey = resolution.SportKey!;

                var syncResult = await SyncFixtureScopeGroupAsync(
                    group.Fixtures,
                    sportKey,
                    options,
                    cancellationToken);

                result.RequestsUsed += syncResult.RequestsUsed;
                result.ProviderEventsReceived += syncResult.ProviderEventsReceived;
                result.FixturesMatched += syncResult.FixturesMatched;
                result.FixturesMissingMatch += syncResult.FixturesMissingMatch;
                result.BookmakersProcessed += syncResult.BookmakersProcessed;
                result.SnapshotsProcessed += syncResult.SnapshotsProcessed;
                result.SnapshotsInserted += syncResult.SnapshotsInserted;
                result.SnapshotsSkippedUnchanged += syncResult.SnapshotsSkippedUnchanged;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The Odds batch live sync failed.");
            result.SkippedReason = "provider_sync_failed";
            result.ProviderError = BuildSafeErrorMessage(ex);
            return result;
        }
    }

    public async Task<TheOddsLiveOddsSyncResultDto> SyncFixtureLiveOddsAsync(
        long apiFixtureId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var fixture = await ResolveFixtureAsync(null, apiFixtureId, cancellationToken);
        if (fixture is null)
        {
            return new TheOddsLiveOddsSyncResultDto
            {
                ApiFixtureId = apiFixtureId,
                ProviderEnabled = _optionsMonitor.CurrentValue.Enabled,
                ProviderConfigured = _optionsMonitor.CurrentValue.IsConfigured(),
                ExecutedAtUtc = DateTime.UtcNow,
                SkippedReason = "fixture_not_found"
            };
        }

        var result = await SyncLeagueLiveOddsAsync(fixture.LeagueApiId, fixture.Season, force, cancellationToken);
        result.ApiFixtureId = fixture.ApiFixtureId;
        return result;
    }

    public async Task<IReadOnlyList<LiveOddsMarketDto>> GetLiveOddsWithCatchUpAsync(
        long? fixtureId = null,
        long? apiFixtureId = null,
        bool latestOnly = true,
        CancellationToken cancellationToken = default)
    {
        var fixture = await ResolveFixtureAsync(fixtureId, apiFixtureId, cancellationToken);
        if (fixture is null)
            return Array.Empty<LiveOddsMarketDto>();

        var stored = await GetStoredLiveOddsAsync(fixture, latestOnly, cancellationToken);
        if (!_optionsMonitor.CurrentValue.ShouldAllowReadDrivenCatchUp())
            return stored;

        if (stored.Count > 0 && !ShouldAttemptRefresh(fixture, stored, _optionsMonitor.CurrentValue.GetFreshnessInterval()))
            return stored;

        if (!ShouldAttemptLiveOddsCatchUp(fixture))
            return stored;

        try
        {
            await SyncLeagueLiveOddsAsync(fixture.LeagueApiId, fixture.Season, force: false, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "The Odds live odds catch-up failed for fixture {ApiFixtureId}. Returning cached data only.",
                fixture.ApiFixtureId);

            return stored;
        }

        var refreshed = await GetStoredLiveOddsAsync(fixture, latestOnly, cancellationToken);
        return refreshed.Count > 0
            ? refreshed
            : stored;
    }

    public async Task<IReadOnlyList<LiveOddsMarketDto>> GetStoredLiveOddsAsync(
        long? fixtureId = null,
        long? apiFixtureId = null,
        bool latestOnly = true,
        CancellationToken cancellationToken = default)
    {
        var fixture = await ResolveFixtureAsync(fixtureId, apiFixtureId, cancellationToken);
        if (fixture is null)
            return Array.Empty<LiveOddsMarketDto>();

        return await GetStoredLiveOddsAsync(fixture, latestOnly, cancellationToken);
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
            .Select(x => new FixtureScope
            {
                FixtureId = x.Id,
                ApiFixtureId = x.ApiFixtureId,
                LeagueApiId = x.League.ApiLeagueId,
                Season = x.Season,
                HomeTeamName = x.HomeTeam.Name,
                AwayTeamName = x.AwayTeam.Name
            })
            .ToListAsync(cancellationToken);

        if (fixtures.Count == 0)
            return Array.Empty<FixtureLiveOddsSummaryDto>();

        var fixtureIds = fixtures.Select(x => x.FixtureId).ToList();

        var rows = await _dbContext.TheOddsLiveOdds
            .AsNoTracking()
            .Where(x => fixtureIds.Contains(x.FixtureId))
            .Where(x => x.MarketName == PreMatchOddsService.DefaultMarketName)
            .Select(x => new StoredLiveOddRow
            {
                FixtureId = x.FixtureId,
                ProviderEventId = x.ProviderEventId,
                BookmakerKey = x.BookmakerKey,
                BookmakerTitle = x.BookmakerTitle,
                MarketKey = x.MarketKey,
                MarketName = x.MarketName,
                OutcomeName = x.OutcomeName,
                Point = x.Point,
                Price = x.Price,
                CollectedAtUtc = x.CollectedAtUtc
            })
            .ToListAsync(cancellationToken);

        var latestRows = rows
            .GroupBy(x => new { x.FixtureId, x.BookmakerKey, x.MarketKey })
            .SelectMany(group =>
            {
                var latestCollectedAtUtc = group.Max(x => x.CollectedAtUtc);
                return group.Where(x => x.CollectedAtUtc == latestCollectedAtUtc);
            })
            .ToList();

        return fixtures
            .Select(fixture => BuildSummary(
                fixture,
                latestRows.Where(x => x.FixtureId == fixture.FixtureId).ToList()))
            .Where(x => x is not null)
            .Cast<FixtureLiveOddsSummaryDto>()
            .OrderBy(x => x.ApiFixtureId)
            .ToList();
    }

    private async Task<IReadOnlyList<LiveOddsMarketDto>> GetStoredLiveOddsAsync(
        FixtureScope fixture,
        bool latestOnly,
        CancellationToken cancellationToken)
    {
        var lastSyncedAtUtc = latestOnly
            ? await _dbContext.SyncStates
                .AsNoTracking()
                .Where(x =>
                    x.EntityType == "the_odds_live_odds" &&
                    x.LeagueApiId == fixture.LeagueApiId &&
                    x.Season == fixture.Season)
                .Select(x => (DateTime?)x.LastSyncedAt)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var rows = await _dbContext.TheOddsLiveOdds
            .AsNoTracking()
            .Where(x => x.FixtureId == fixture.FixtureId)
            .Where(x => x.MarketName == PreMatchOddsService.DefaultMarketName)
            .OrderByDescending(x => x.CollectedAtUtc)
            .ThenBy(x => x.BookmakerTitle)
            .ThenBy(x => x.OutcomeName)
            .Select(x => new StoredLiveOddRow
            {
                FixtureId = x.FixtureId,
                ProviderEventId = x.ProviderEventId,
                BookmakerKey = x.BookmakerKey,
                BookmakerTitle = x.BookmakerTitle,
                MarketKey = x.MarketKey,
                MarketName = x.MarketName,
                OutcomeName = x.OutcomeName,
                Point = x.Point,
                Price = x.Price,
                CollectedAtUtc = x.CollectedAtUtc
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return Array.Empty<LiveOddsMarketDto>();

        var grouped = rows
            .GroupBy(x => new { x.ProviderEventId, x.BookmakerKey, x.BookmakerTitle, x.MarketKey, x.MarketName });

        var result = new List<LiveOddsMarketDto>();

        foreach (var group in grouped)
        {
            var lastSnapshotCollectedAtUtc = group.Max(x => x.CollectedAtUtc);
            var selectedRows = latestOnly
                ? group.Where(x => x.CollectedAtUtc == lastSnapshotCollectedAtUtc).ToList()
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
                SourceProvider = "the-odds-api",
                ExternalEventId = group.Key.ProviderEventId,
                ExternalBookmakerKey = group.Key.BookmakerKey,
                ExternalMarketKey = group.Key.MarketKey,
                BookmakerId = 0,
                ApiBookmakerId = 0,
                Bookmaker = group.Key.BookmakerTitle,
                BookmakerIdentityType = "external",
                ApiBetId = 0,
                BetName = group.Key.MarketName,
                CollectedAtUtc = effectiveCollectedAtUtc,
                LastSnapshotCollectedAtUtc = lastSnapshotCollectedAtUtc,
                LastSyncedAtUtc = lastSyncedAtUtc,
                Values = selectedRows
                    .OrderBy(x => x.OutcomeName)
                    .ThenBy(x => x.Point)
                    .Select(x => new LiveOddsValueDto
                    {
                        OutcomeLabel = x.OutcomeName,
                        Line = x.Point?.ToString(CultureInfo.InvariantCulture),
                        Odd = x.Price
                    })
                    .ToList()
            });
        }

        return result
            .OrderBy(x => x.Bookmaker)
            .ThenBy(x => x.BetName)
            .ToList();
    }

    private async Task<TheOddsInternalSyncResult> SyncFixtureScopeGroupAsync(
        IReadOnlyList<FixtureScope> fixtures,
        string sportKey,
        TheOddsApiOptions options,
        CancellationToken cancellationToken)
    {
        var result = new TheOddsInternalSyncResult
        {
            SportKey = sportKey
        };

        if (fixtures.Count == 0)
            return result;

        var matchTolerance = options.GetMatchTolerance();
        var commenceTimeFromUtc = fixtures.Min(x => x.KickoffAtUtc).Subtract(matchTolerance);
        var commenceTimeToUtc = fixtures.Max(x => x.KickoffAtUtc).Add(matchTolerance);

        var providerRows = await _apiService.GetLiveH2HOddsAsync(
            sportKey,
            commenceTimeFromUtc,
            commenceTimeToUtc,
            cancellationToken);

        result.RequestsUsed = 1;
        result.ProviderEventsReceived = providerRows.Count;

        if (providerRows.Count == 0)
            return result;

        var fixtureIds = fixtures.Select(x => x.FixtureId).ToList();
        var latestRows = await _dbContext.TheOddsLiveOdds
            .AsNoTracking()
            .Where(x => fixtureIds.Contains(x.FixtureId) && x.MarketKey == options.GetMarketKey())
            .OrderByDescending(x => x.CollectedAtUtc)
            .Select(x => new SnapshotRow
            {
                FixtureId = x.FixtureId,
                BookmakerKey = x.BookmakerKey,
                MarketKey = x.MarketKey,
                OutcomeName = x.OutcomeName,
                Point = x.Point,
                Price = x.Price,
                LastUpdateUtc = x.LastUpdateUtc,
                CollectedAtUtc = x.CollectedAtUtc
            })
            .ToListAsync(cancellationToken);

        var latestByKey = latestRows
            .GroupBy(x => BuildSnapshotKey(x.FixtureId, x.BookmakerKey, x.MarketKey, x.OutcomeName, x.Point))
            .ToDictionary(x => x.Key, x => x.First());

        var nowUtc = DateTime.UtcNow;

        foreach (var providerEvent in providerRows)
        {
            var fixture = MatchFixture(providerEvent, fixtures, matchTolerance);
            if (fixture is null)
            {
                result.FixturesMissingMatch++;
                continue;
            }

            result.FixturesMatched++;

            foreach (var bookmaker in providerEvent.Bookmakers.OrderBy(x => x.Title))
            {
                var market = bookmaker.Markets.FirstOrDefault(x =>
                    string.Equals(x.Key, options.GetMarketKey(), StringComparison.OrdinalIgnoreCase));
                if (market is null)
                    continue;

                result.BookmakersProcessed++;

                foreach (var outcome in market.Outcomes)
                {
                    if (!outcome.Price.HasValue)
                        continue;

                    result.SnapshotsProcessed++;

                    var normalizedOutcomeName = NormalizeNullable(outcome.Name) ?? string.Empty;
                    var snapshotKey = BuildSnapshotKey(
                        fixture.FixtureId,
                        bookmaker.Key,
                        market.Key,
                        normalizedOutcomeName,
                        outcome.Point);

                    if (latestByKey.TryGetValue(snapshotKey, out var latest) &&
                        latest.Price == outcome.Price &&
                        latest.Point == outcome.Point &&
                        latest.LastUpdateUtc == market.LastUpdate)
                    {
                        result.SnapshotsSkippedUnchanged++;
                        continue;
                    }

                    _dbContext.TheOddsLiveOdds.Add(new TheOddsLiveOdd
                    {
                        FixtureId = fixture.FixtureId,
                        ProviderEventId = providerEvent.Id,
                        SportKey = providerEvent.SportKey,
                        BookmakerKey = bookmaker.Key,
                        BookmakerTitle = bookmaker.Title.Trim(),
                        MarketKey = market.Key,
                        MarketName = PreMatchOddsService.DefaultMarketName,
                        OutcomeName = normalizedOutcomeName,
                        Point = outcome.Point,
                        Price = outcome.Price,
                        LastUpdateUtc = market.LastUpdate ?? bookmaker.LastUpdate,
                        CollectedAtUtc = nowUtc
                    });

                    latestByKey[snapshotKey] = new SnapshotRow
                    {
                        FixtureId = fixture.FixtureId,
                        BookmakerKey = bookmaker.Key,
                        MarketKey = market.Key,
                        OutcomeName = normalizedOutcomeName,
                        Point = outcome.Point,
                        Price = outcome.Price,
                        LastUpdateUtc = market.LastUpdate ?? bookmaker.LastUpdate,
                        CollectedAtUtc = nowUtc
                    };

                    result.SnapshotsInserted++;
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();

        var syncStateItems = fixtures
            .Select(x => new { x.LeagueApiId, x.Season })
            .Distinct()
            .Select(x => new SyncStateUpsertItem
            {
                EntityType = "the_odds_live_odds",
                LeagueApiId = x.LeagueApiId,
                Season = x.Season,
                SyncedAtUtc = nowUtc
            })
            .ToList();

        if (syncStateItems.Count > 0)
        {
            await _syncStateService.SetLastSyncedAtBatchAsync(syncStateItems, cancellationToken);
        }

        return result;
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
            .Select(x => new FixtureScope
            {
                FixtureId = x.Id,
                ApiFixtureId = x.ApiFixtureId,
                LeagueApiId = x.League.ApiLeagueId,
                Season = x.Season,
                KickoffAtUtc = x.KickoffAt,
                Status = x.Status,
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

    private async Task<bool> HasRecentLeagueSyncAsync(
        long leagueApiId,
        int season,
        TimeSpan minInterval,
        CancellationToken cancellationToken)
    {
        var lastSyncedAtUtc = await _dbContext.SyncStates
            .AsNoTracking()
            .Where(x =>
                x.EntityType == "the_odds_live_odds" &&
                x.LeagueApiId == leagueApiId &&
                x.Season == season)
            .Select(x => (DateTime?)x.LastSyncedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return lastSyncedAtUtc.HasValue &&
               DateTime.UtcNow - lastSyncedAtUtc.Value < minInterval;
    }

    private static FixtureScope? MatchFixture(
        TheOddsApiOddsItem providerEvent,
        IReadOnlyList<FixtureScope> fixtures,
        TimeSpan tolerance)
    {
        var normalizedHomeTeam = NormalizeTeamName(providerEvent.HomeTeam);
        var normalizedAwayTeam = NormalizeTeamName(providerEvent.AwayTeam);

        return fixtures
            .Where(x =>
                NormalizeTeamName(x.HomeTeamName) == normalizedHomeTeam &&
                NormalizeTeamName(x.AwayTeamName) == normalizedAwayTeam)
            .Select(x => new
            {
                Fixture = x,
                KickoffDelta = Math.Abs((x.KickoffAtUtc - providerEvent.CommenceTime).TotalMinutes)
            })
            .Where(x => x.KickoffDelta <= tolerance.TotalMinutes)
            .OrderBy(x => x.KickoffDelta)
            .Select(x => x.Fixture)
            .FirstOrDefault();
    }

    private static string NormalizeTeamName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToUpperInvariant(c));
            }
        }

        var normalizedTeamName = builder.ToString();

        return TeamNameAliases.TryGetValue(normalizedTeamName, out var canonical)
            ? canonical
            : normalizedTeamName;
    }

    private static bool ShouldAttemptRefresh(
        FixtureScope fixture,
        IReadOnlyList<LiveOddsMarketDto> stored,
        TimeSpan freshnessInterval)
    {
        if (!ShouldAttemptLiveOddsCatchUp(fixture))
            return false;

        var latestCollectedAtUtc = stored.Max(x => x.CollectedAtUtc);
        return DateTime.UtcNow - latestCollectedAtUtc >= freshnessInterval;
    }

    private static bool ShouldAttemptLiveOddsCatchUp(FixtureScope fixture)
    {
        var bucket = FixtureStatusMapper.GetStateBucket(fixture.Status);
        if (bucket is FixtureStateBucket.Postponed or FixtureStateBucket.Cancelled or FixtureStateBucket.Finished)
            return false;

        var nowUtc = DateTime.UtcNow;
        return fixture.KickoffAtUtc >= nowUtc.AddHours(-4) &&
               fixture.KickoffAtUtc <= nowUtc.AddMinutes(15);
    }

    private static FixtureLiveOddsSummaryDto? BuildSummary(
        FixtureScope fixture,
        IReadOnlyList<StoredLiveOddRow> rows)
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

        foreach (var row in rows)
        {
            if (IsHomeOutcome(row.OutcomeName, fixture.HomeTeamName))
            {
                ApplyBestOdd(row.Price, row.BookmakerTitle, summary.BestHomeOdd, summary.BestHomeBookmaker,
                    (odd, bookmaker) =>
                    {
                        summary.BestHomeOdd = odd;
                        summary.BestHomeBookmaker = bookmaker;
                    });
            }
            else if (IsDrawOutcome(row.OutcomeName))
            {
                ApplyBestOdd(row.Price, row.BookmakerTitle, summary.BestDrawOdd, summary.BestDrawBookmaker,
                    (odd, bookmaker) =>
                    {
                        summary.BestDrawOdd = odd;
                        summary.BestDrawBookmaker = bookmaker;
                    });
            }
            else if (IsAwayOutcome(row.OutcomeName, fixture.AwayTeamName))
            {
                ApplyBestOdd(row.Price, row.BookmakerTitle, summary.BestAwayOdd, summary.BestAwayBookmaker,
                    (odd, bookmaker) =>
                    {
                        summary.BestAwayOdd = odd;
                        summary.BestAwayBookmaker = bookmaker;
                    });
            }
        }

        return summary.BestHomeOdd.HasValue || summary.BestDrawOdd.HasValue || summary.BestAwayOdd.HasValue
            ? summary
            : null;
    }

    private static bool IsHomeOutcome(string? outcomeLabel, string homeTeamName)
    {
        if (string.IsNullOrWhiteSpace(outcomeLabel))
            return false;

        return string.Equals(
            NormalizeTeamName(outcomeLabel),
            NormalizeTeamName(homeTeamName),
            StringComparison.Ordinal);
    }

    private static bool IsDrawOutcome(string? outcomeLabel)
    {
        if (string.IsNullOrWhiteSpace(outcomeLabel))
            return false;

        var normalized = outcomeLabel.Trim().ToUpperInvariant();
        return normalized is "DRAW" or "TIE" or "X";
    }

    private static bool IsAwayOutcome(string? outcomeLabel, string awayTeamName)
    {
        if (string.IsNullOrWhiteSpace(outcomeLabel))
            return false;

        return string.Equals(
            NormalizeTeamName(outcomeLabel),
            NormalizeTeamName(awayTeamName),
            StringComparison.Ordinal);
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

    private static string BuildSnapshotKey(
        long fixtureId,
        string bookmakerKey,
        string marketKey,
        string outcomeName,
        decimal? point)
    {
        return $"{fixtureId}:{bookmakerKey}:{marketKey}:{outcomeName}:{point?.ToString(CultureInfo.InvariantCulture) ?? string.Empty}".ToUpperInvariant();
    }

    private static string? NormalizeNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private static string BuildSafeErrorMessage(Exception ex)
    {
        var message = ex.GetBaseException().Message;
        if (string.IsNullOrWhiteSpace(message))
            return ex.GetType().Name;

        return message.Length <= 500
            ? message
            : message[..500];
    }

    private sealed class FixtureScope
    {
        public long FixtureId { get; set; }
        public long ApiFixtureId { get; set; }
        public long LeagueApiId { get; set; }
        public int Season { get; set; }
        public DateTime KickoffAtUtc { get; set; }
        public string? Status { get; set; }
        public string HomeTeamName { get; set; } = string.Empty;
        public string AwayTeamName { get; set; } = string.Empty;
    }

    private sealed class SnapshotRow
    {
        public long FixtureId { get; set; }
        public string BookmakerKey { get; set; } = string.Empty;
        public string MarketKey { get; set; } = string.Empty;
        public string OutcomeName { get; set; } = string.Empty;
        public decimal? Point { get; set; }
        public decimal? Price { get; set; }
        public DateTime? LastUpdateUtc { get; set; }
        public DateTime CollectedAtUtc { get; set; }
    }

    private sealed class StoredLiveOddRow
    {
        public long FixtureId { get; set; }
        public string ProviderEventId { get; set; } = string.Empty;
        public string BookmakerKey { get; set; } = string.Empty;
        public string BookmakerTitle { get; set; } = string.Empty;
        public string MarketKey { get; set; } = string.Empty;
        public string MarketName { get; set; } = string.Empty;
        public string OutcomeName { get; set; } = string.Empty;
        public decimal? Point { get; set; }
        public decimal? Price { get; set; }
        public DateTime CollectedAtUtc { get; set; }
    }

    private sealed class TheOddsInternalSyncResult
    {
        public string SportKey { get; set; } = string.Empty;
        public int RequestsUsed { get; set; }
        public int ProviderEventsReceived { get; set; }
        public int FixturesMatched { get; set; }
        public int FixturesMissingMatch { get; set; }
        public int BookmakersProcessed { get; set; }
        public int SnapshotsProcessed { get; set; }
        public int SnapshotsInserted { get; set; }
        public int SnapshotsSkippedUnchanged { get; set; }
    }
}

public class TheOddsLiveOddsBatchSyncResult
{
    public bool ProviderEnabled { get; set; }
    public bool ProviderConfigured { get; set; }
    public string? SkippedReason { get; set; }
    public string? ProviderError { get; set; }
    public int FixturesRequested { get; set; }
    public int LiveFixturesResolved { get; set; }
    public int LeaguesRequested { get; set; }
    public int LeaguesSkippedRecentlySynced { get; set; }
    public int LeaguesMissingSportKeyMapping { get; set; }
    public int LeaguesUnresolvedSportKey { get; set; }
    public int RequestsUsed { get; set; }
    public int ProviderEventsReceived { get; set; }
    public int FixturesMatched { get; set; }
    public int FixturesMissingMatch { get; set; }
    public int BookmakersProcessed { get; set; }
    public int SnapshotsProcessed { get; set; }
    public int SnapshotsInserted { get; set; }
    public int SnapshotsSkippedUnchanged { get; set; }
    public DateTime ExecutedAtUtc { get; set; }
}
