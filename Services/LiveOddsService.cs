using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Entities;

namespace SmartBets.Services;

public class LiveOddsService
{
    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _apiService;
    private readonly SyncStateService _syncStateService;

    public LiveOddsService(
        AppDbContext dbContext,
        FootballApiService apiService,
        SyncStateService syncStateService)
    {
        _dbContext = dbContext;
        _apiService = apiService;
        _syncStateService = syncStateService;
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

    public async Task<IReadOnlyList<OddDto>> GetMatchWinnerOddsAsync(
        long? fixtureId = null,
        long? apiFixtureId = null,
        bool latestOnly = true,
        CancellationToken cancellationToken = default)
    {
        var fixture = await ResolveFixtureAsync(fixtureId, apiFixtureId, cancellationToken);
        if (fixture is null)
            return Array.Empty<OddDto>();

        var markets = await GetLiveOddsAsync(
            fixtureId,
            apiFixtureId,
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

    private sealed record LeagueSeasonScope(long LeagueApiId, int Season);
}
