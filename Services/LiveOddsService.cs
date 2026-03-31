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

        foreach (var scope in touchedScopes)
        {
            var parts = scope.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                continue;

            if (long.TryParse(parts[0], out var parsedLeagueId) &&
                int.TryParse(parts[1], out var parsedSeason))
            {
                await _syncStateService.SetLastSyncedAtAsync(
                    "live_odds",
                    parsedLeagueId,
                    parsedSeason,
                    collectedAtUtc,
                    cancellationToken);
            }
        }

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

            result.Add(new LiveOddsMarketDto
            {
                FixtureId = fixture.FixtureId,
                ApiFixtureId = fixture.ApiFixtureId,
                BookmakerId = group.Key.BookmakerId,
                ApiBookmakerId = group.Key.ApiBookmakerId,
                Bookmaker = group.Key.Name,
                ApiBetId = group.Key.ApiBetId,
                BetName = group.Key.BetName,
                CollectedAtUtc = selectedRows.Max(x => x.CollectedAtUtc),
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
                Season = x.Season
            })
            .AsQueryable();

        if (fixtureId.HasValue)
            query = query.Where(x => x.FixtureId == fixtureId.Value);

        if (apiFixtureId.HasValue)
            query = query.Where(x => x.ApiFixtureId == apiFixtureId.Value);

        return await query.FirstOrDefaultAsync(cancellationToken);
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
}
