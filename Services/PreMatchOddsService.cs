using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Entities;

namespace SmartBets.Services;

public class OddsSyncResult
{
    public string MarketName { get; set; } = string.Empty;
    public int FixturesMatched { get; set; }
    public int FixturesMissingInDatabase { get; set; }
    public int BookmakersProcessed { get; set; }
    public int BookmakersInserted { get; set; }
    public int BookmakersUpdated { get; set; }
    public int SnapshotsProcessed { get; set; }
    public int SnapshotsInserted { get; set; }
    public int SnapshotsSkippedUnchanged { get; set; }
    public int SnapshotsSkippedUnsupportedMarket { get; set; }
    public IReadOnlyList<LeagueSeasonSyncScope> TouchedScopes { get; set; } = Array.Empty<LeagueSeasonSyncScope>();
}

public class PreMatchOddsService
{
    public const string DefaultMarketName = "Match Winner";

    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _apiService;
    private readonly LeagueCoverageService _leagueCoverageService;
    private readonly OddsAnalyticsService _oddsAnalyticsService;
    private readonly PreMatchOddsAttemptTrackerService _attemptTracker;
    private readonly ILogger<PreMatchOddsService> _logger;

    public PreMatchOddsService(
        AppDbContext dbContext,
        FootballApiService apiService,
        LeagueCoverageService leagueCoverageService,
        OddsAnalyticsService oddsAnalyticsService,
        PreMatchOddsAttemptTrackerService attemptTracker,
        ILogger<PreMatchOddsService> logger)
    {
        _dbContext = dbContext;
        _apiService = apiService;
        _leagueCoverageService = leagueCoverageService;
        _oddsAnalyticsService = oddsAnalyticsService;
        _attemptTracker = attemptTracker;
        _logger = logger;
    }

    public async Task<OddsSyncResult> SyncOddsAsync(
        long leagueId,
        int season,
        string? marketName = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedMarketName = NormalizeMarketName(marketName);

        await _leagueCoverageService.EnsureOddsSupportedAsync(leagueId, season, cancellationToken);

        var leagueExists = await _dbContext.Leagues
            .AsNoTracking()
            .AnyAsync(x => x.ApiLeagueId == leagueId && x.Season == season, cancellationToken);

        if (!leagueExists)
        {
            throw new InvalidOperationException(
                $"League with apiLeagueId {leagueId} and season {season} was not found in database.");
        }

        var apiOddsFixtures = await _apiService.GetOddsAsync(leagueId, season, cancellationToken);
        return await SyncOddsCoreAsync(apiOddsFixtures, normalizedMarketName, cancellationToken);
    }

    public async Task<OddsSyncResult> SyncOddsForFixturesAsync(
        IReadOnlyCollection<long> apiFixtureIds,
        string? marketName = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedFixtureIds = apiFixtureIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var normalizedMarketName = NormalizeMarketName(marketName);
        if (normalizedFixtureIds.Count == 0)
        {
            return new OddsSyncResult
            {
                MarketName = normalizedMarketName
            };
        }

        var apiOddsFixtures = new List<Models.ApiFootball.ApiFootballOddsFixtureItem>();
        var providerEmptyResponses = 0;
        var failedFixtureRequests = 0;

        foreach (var apiFixtureId in normalizedFixtureIds)
        {
            try
            {
                var rows = await _apiService.GetOddsByFixtureAsync(apiFixtureId, cancellationToken);
                if (rows.Count > 0)
                {
                    apiOddsFixtures.AddRange(rows);
                }
                else
                {
                    providerEmptyResponses++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failedFixtureRequests++;
                _logger.LogWarning(
                    ex,
                    "Pre-match odds fetch failed for fixture {ApiFixtureId}. Continuing with the remaining fixtures in the batch.",
                    apiFixtureId);
            }
            finally
            {
                _attemptTracker.RecordAttempt(apiFixtureId, DateTime.UtcNow);
            }
        }

        if (providerEmptyResponses > 0 || failedFixtureRequests > 0)
        {
            _logger.LogInformation(
                "Pre-match odds fixture batch completed with partial provider coverage. Attempted={Attempted}, WithRows={WithRows}, Empty={Empty}, Failed={Failed}.",
                normalizedFixtureIds.Count,
                Math.Max(0, normalizedFixtureIds.Count - providerEmptyResponses - failedFixtureRequests),
                providerEmptyResponses,
                failedFixtureRequests);
        }

        return await SyncOddsCoreAsync(apiOddsFixtures, normalizedMarketName, cancellationToken);
    }

    public async Task<IReadOnlyList<OddDto>> GetFixtureOddsAsync(
        long? fixtureId = null,
        long? apiFixtureId = null,
        string? marketName = null,
        bool latestOnly = true,
        CancellationToken cancellationToken = default)
    {
        var fixtureReference = await ResolveFixtureReferenceAsync(fixtureId, apiFixtureId, cancellationToken);
        if (fixtureReference is null)
            return Array.Empty<OddDto>();

        var normalizedMarketName = string.IsNullOrWhiteSpace(marketName)
            ? null
            : NormalizeMarketName(marketName);

        var odds = await _dbContext.PreMatchOdds
            .AsNoTracking()
            .Where(x => x.FixtureId == fixtureReference.Id)
            .Where(x => normalizedMarketName == null || x.MarketName == normalizedMarketName)
            .Select(x => new OddDto
            {
                FixtureId = x.FixtureId,
                ApiFixtureId = fixtureReference.ApiFixtureId,
                BookmakerId = x.BookmakerId,
                ApiBookmakerId = x.Bookmaker.ApiBookmakerId,
                Bookmaker = x.Bookmaker.Name,
                MarketName = x.MarketName,
                HomeOdd = x.HomeOdd,
                DrawOdd = x.DrawOdd,
                AwayOdd = x.AwayOdd,
                CollectedAtUtc = x.CollectedAt
            })
            .OrderByDescending(x => x.CollectedAtUtc)
            .ThenBy(x => x.Bookmaker)
            .ToListAsync(cancellationToken);

        if (!latestOnly)
            return odds;

        return odds
            .GroupBy(x => $"{x.ApiBookmakerId}:{x.MarketName}", StringComparer.OrdinalIgnoreCase)
            .Select(x =>
            {
                var ordered = x
                    .OrderByDescending(y => y.CollectedAtUtc)
                    .ThenBy(y => y.Bookmaker)
                    .ToList();

                if (string.Equals(ordered[0].MarketName, DefaultMarketName, StringComparison.OrdinalIgnoreCase))
                {
                    return ordered.FirstOrDefault(IsCompleteMatchWinnerOdds) ?? ordered[0];
                }

                return ordered[0];
            })
            .OrderBy(x => x.Bookmaker)
            .ThenBy(x => x.MarketName)
            .ToList();
    }

    public async Task<BestOddsDto?> GetBestOddsAsync(
        long? fixtureId = null,
        long? apiFixtureId = null,
        string? marketName = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedMarketName = NormalizeMarketName(marketName);
        var odds = await GetFixtureOddsAsync(
            fixtureId,
            apiFixtureId,
            normalizedMarketName,
            latestOnly: true,
            cancellationToken);

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
            MarketName = normalizedMarketName,
            CollectedAtUtc = odds.Max(x => x.CollectedAtUtc),
            BestHomeOdd = bestHome?.HomeOdd,
            BestHomeBookmaker = bestHome?.Bookmaker,
            BestDrawOdd = bestDraw?.DrawOdd,
            BestDrawBookmaker = bestDraw?.Bookmaker,
            BestAwayOdd = bestAway?.AwayOdd,
            BestAwayBookmaker = bestAway?.Bookmaker
        };
    }

    public async Task<DateTime?> GetLatestCollectedAtUtcAsync(
        long? fixtureId = null,
        long? apiFixtureId = null,
        string? marketName = null,
        CancellationToken cancellationToken = default)
    {
        var fixtureReference = await ResolveFixtureReferenceAsync(fixtureId, apiFixtureId, cancellationToken);
        if (fixtureReference is null)
            return null;

        var normalizedMarketName = string.IsNullOrWhiteSpace(marketName)
            ? null
            : NormalizeMarketName(marketName);

        return await _dbContext.PreMatchOdds
            .AsNoTracking()
            .Where(x => x.FixtureId == fixtureReference.Id)
            .Where(x => normalizedMarketName == null || x.MarketName == normalizedMarketName)
            .MaxAsync(x => (DateTime?)x.CollectedAt, cancellationToken);
    }

    private async Task<FixtureReference?> ResolveFixtureReferenceAsync(
        long? fixtureId,
        long? apiFixtureId,
        CancellationToken cancellationToken)
    {
        if (!fixtureId.HasValue && !apiFixtureId.HasValue)
            return null;

        var query = _dbContext.Fixtures
            .AsNoTracking()
            .Select(x => new FixtureReference
            {
                Id = x.Id,
                ApiFixtureId = x.ApiFixtureId,
                HomeTeamName = x.HomeTeam.Name,
                AwayTeamName = x.AwayTeam.Name
            })
            .AsQueryable();

        if (fixtureId.HasValue)
        {
            query = query.Where(x => x.Id == fixtureId.Value);
        }

        if (apiFixtureId.HasValue)
        {
            query = query.Where(x => x.ApiFixtureId == apiFixtureId.Value);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private static string NormalizeMarketName(string? marketName)
    {
        return string.IsNullOrWhiteSpace(marketName)
            ? DefaultMarketName
            : marketName.Trim();
    }

    private static bool TryExtractThreeWayOdds(
        Models.ApiFootball.ApiFootballOddsBookmaker apiBookmaker,
        string marketName,
        string homeTeamName,
        string awayTeamName,
        out decimal? homeOdd,
        out decimal? drawOdd,
        out decimal? awayOdd)
    {
        homeOdd = null;
        drawOdd = null;
        awayOdd = null;

        var bet = apiBookmaker.Bets
            .FirstOrDefault(x => string.Equals(x.Name?.Trim(), marketName, StringComparison.OrdinalIgnoreCase));

        if (bet is null)
            return false;

        foreach (var value in bet.Values)
        {
            if (!TryParseOdd(value.Odd, out var oddValue))
                continue;

            if (string.IsNullOrWhiteSpace(value.Value))
                continue;

            if (MatchOutcomeNormalizer.IsHomeOutcome(value.Value, homeTeamName))
            {
                homeOdd = oddValue;
                continue;
            }

            if (MatchOutcomeNormalizer.IsDrawOutcome(value.Value))
            {
                drawOdd = oddValue;
                continue;
            }

            if (MatchOutcomeNormalizer.IsAwayOutcome(value.Value, awayTeamName))
            {
                awayOdd = oddValue;
            }
        }

        return homeOdd.HasValue || drawOdd.HasValue || awayOdd.HasValue;
    }

    private static bool TryParseOdd(string? value, out decimal odd)
    {
        return decimal.TryParse(
            value,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out odd);
    }

    private static string BuildSnapshotKey(long fixtureId, long apiBookmakerId, string marketName)
    {
        return $"{fixtureId}:{apiBookmakerId}:{marketName}".ToUpperInvariant();
    }

    private static bool IsCompleteThreeWayOdds(decimal? homeOdd, decimal? drawOdd, decimal? awayOdd)
    {
        return homeOdd.HasValue && drawOdd.HasValue && awayOdd.HasValue;
    }

    private static bool IsCompleteMatchWinnerOdds(OddDto odd)
    {
        return IsCompleteThreeWayOdds(odd.HomeOdd, odd.DrawOdd, odd.AwayOdd);
    }

    private async Task<OddsSyncResult> SyncOddsCoreAsync(
        IReadOnlyCollection<Models.ApiFootball.ApiFootballOddsFixtureItem> apiOddsFixtures,
        string normalizedMarketName,
        CancellationToken cancellationToken)
    {
        var apiFixtureIds = apiOddsFixtures
            .Select(x => x.Fixture.Id)
            .Distinct()
            .ToList();

        var fixtures = await _dbContext.Fixtures
            .AsNoTracking()
            .Where(x => apiFixtureIds.Contains(x.ApiFixtureId))
            .Select(x => new FixtureReference
            {
                Id = x.Id,
                ApiFixtureId = x.ApiFixtureId,
                HomeTeamName = x.HomeTeam.Name,
                AwayTeamName = x.AwayTeam.Name,
                LeagueApiId = x.League.ApiLeagueId,
                Season = x.Season
            })
            .ToListAsync(cancellationToken);

        var fixturesByApiId = fixtures.ToDictionary(x => x.ApiFixtureId, x => x);
        var localFixtureIds = fixtures.Select(x => x.Id).ToList();

        var existingBookmakers = await _dbContext.Bookmakers.ToListAsync(cancellationToken);
        var bookmakersByApiId = existingBookmakers.ToDictionary(x => x.ApiBookmakerId, x => x);

        var latestOddsSnapshots = await _dbContext.PreMatchOdds
            .AsNoTracking()
            .Where(x => localFixtureIds.Contains(x.FixtureId) && x.MarketName == normalizedMarketName)
            .Select(x => new LatestOddsSnapshot
            {
                FixtureId = x.FixtureId,
                ApiBookmakerId = x.Bookmaker.ApiBookmakerId,
                MarketName = x.MarketName,
                HomeOdd = x.HomeOdd,
                DrawOdd = x.DrawOdd,
                AwayOdd = x.AwayOdd,
                CollectedAtUtc = x.CollectedAt
            })
            .OrderByDescending(x => x.CollectedAtUtc)
            .ToListAsync(cancellationToken);

        var latestByKey = latestOddsSnapshots
            .GroupBy(x => BuildSnapshotKey(x.FixtureId, x.ApiBookmakerId, x.MarketName))
            .ToDictionary(x => x.Key, x => x.First());

        var collectedAtUtc = DateTime.UtcNow;
        var touchedFixtureIds = new HashSet<long>();
        var touchedScopes = new HashSet<string>(StringComparer.Ordinal);

        var result = new OddsSyncResult
        {
            MarketName = normalizedMarketName
        };

        foreach (var fixtureItem in apiOddsFixtures)
        {
            if (!fixturesByApiId.TryGetValue(fixtureItem.Fixture.Id, out var fixture))
            {
                result.FixturesMissingInDatabase++;
                continue;
            }

            result.FixturesMatched++;
            touchedFixtureIds.Add(fixture.Id);
            touchedScopes.Add($"{fixture.LeagueApiId}:{fixture.Season}");

            foreach (var apiBookmaker in fixtureItem.Bookmakers)
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

                if (!TryExtractThreeWayOdds(
                        apiBookmaker,
                        normalizedMarketName,
                        fixture.HomeTeamName,
                        fixture.AwayTeamName,
                        out var homeOdd,
                        out var drawOdd,
                        out var awayOdd))
                {
                    result.SnapshotsSkippedUnsupportedMarket++;
                    continue;
                }

                if (string.Equals(normalizedMarketName, DefaultMarketName, StringComparison.OrdinalIgnoreCase) &&
                    !IsCompleteThreeWayOdds(homeOdd, drawOdd, awayOdd))
                {
                    result.SnapshotsSkippedUnsupportedMarket++;
                    continue;
                }

                result.SnapshotsProcessed++;

                var snapshotKey = BuildSnapshotKey(fixture.Id, apiBookmaker.Id, normalizedMarketName);
                if (latestByKey.TryGetValue(snapshotKey, out var latestSnapshot) &&
                    latestSnapshot.HomeOdd == homeOdd &&
                    latestSnapshot.DrawOdd == drawOdd &&
                    latestSnapshot.AwayOdd == awayOdd)
                {
                    result.SnapshotsSkippedUnchanged++;
                    continue;
                }

                _dbContext.PreMatchOdds.Add(new PreMatchOdd
                {
                    FixtureId = fixture.Id,
                    Bookmaker = bookmaker,
                    MarketName = normalizedMarketName,
                    HomeOdd = homeOdd,
                    DrawOdd = drawOdd,
                    AwayOdd = awayOdd,
                    CollectedAt = collectedAtUtc
                });

                latestByKey[snapshotKey] = new LatestOddsSnapshot
                {
                    FixtureId = fixture.Id,
                    ApiBookmakerId = apiBookmaker.Id,
                    MarketName = normalizedMarketName,
                    HomeOdd = homeOdd,
                    DrawOdd = drawOdd,
                    AwayOdd = awayOdd,
                    CollectedAtUtc = collectedAtUtc
                };

                result.SnapshotsInserted++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();

        if (touchedFixtureIds.Count > 0)
        {
            await _oddsAnalyticsService.RebuildForFixtureIdsAsync(
                touchedFixtureIds,
                normalizedMarketName,
                cancellationToken);
        }

        result.TouchedScopes = touchedScopes
            .Select(ParseLeagueSeasonSyncScope)
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderBy(x => x.LeagueApiId)
            .ThenBy(x => x.Season)
            .ToList();

        return result;
    }

    private static LeagueSeasonSyncScope? ParseLeagueSeasonSyncScope(string scope)
    {
        var parts = scope.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return null;

        if (!long.TryParse(parts[0], out var leagueApiId) || !int.TryParse(parts[1], out var season))
            return null;

        return new LeagueSeasonSyncScope
        {
            LeagueApiId = leagueApiId,
            Season = season
        };
    }

    private sealed class FixtureReference
    {
        public long Id { get; set; }
        public long ApiFixtureId { get; set; }
        public string HomeTeamName { get; set; } = string.Empty;
        public string AwayTeamName { get; set; } = string.Empty;
        public long LeagueApiId { get; set; }
        public int Season { get; set; }
    }

    private sealed class LatestOddsSnapshot
    {
        public long FixtureId { get; set; }
        public long ApiBookmakerId { get; set; }
        public string MarketName { get; set; } = string.Empty;
        public decimal? HomeOdd { get; set; }
        public decimal? DrawOdd { get; set; }
        public decimal? AwayOdd { get; set; }
        public DateTime CollectedAtUtc { get; set; }
    }
}

public class LeagueSeasonSyncScope
{
    public long LeagueApiId { get; set; }
    public int Season { get; set; }
}
