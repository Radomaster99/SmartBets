using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Entities;

namespace SmartBets.Services;

public class FixtureLiveStatusSyncService
{
    private static readonly string[] UpcomingStatuses = FixtureStatusMapper
        .GetStatusesForBucket(Enums.FixtureStateBucket.Upcoming)
        .ToArray();

    private static readonly string[] LiveStatuses = FixtureStatusMapper
        .GetStatusesForBucket(Enums.FixtureStateBucket.Live)
        .ToArray();

    private static readonly TimeSpan CatchUpLookbackWindow = TimeSpan.FromHours(6);
    private static readonly TimeSpan CatchUpLookaheadWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan CatchUpMinResyncInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan EndgameCatchUpMinResyncInterval = TimeSpan.FromMinutes(1);
    private const int CatchUpBatchSize = 20;

    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _apiService;
    private readonly SyncStateService _syncStateService;
    private readonly IOptionsMonitor<CoreDataAutomationOptions> _automationOptionsMonitor;

    public FixtureLiveStatusSyncService(
        AppDbContext dbContext,
        FootballApiService apiService,
        SyncStateService syncStateService,
        IOptionsMonitor<CoreDataAutomationOptions> automationOptionsMonitor)
    {
        _dbContext = dbContext;
        _apiService = apiService;
        _syncStateService = syncStateService;
        _automationOptionsMonitor = automationOptionsMonitor;
    }

    public async Task<LiveFixtureStatusSyncResultDto> SyncLiveFixturesAsync(
        long? leagueId = null,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var targetLeagueIds = await ResolveTargetLeagueIdsAsync(leagueId, activeOnly, cancellationToken);

        if (leagueId is null && activeOnly && targetLeagueIds.Count == 0)
        {
            return new LiveFixtureStatusSyncResultDto
            {
                ScopedToActiveSupportedLeagues = true,
                RequestsUsed = 0,
                ExecutedAtUtc = nowUtc
            };
        }

        var liveFixtures = await _apiService.GetLiveFixturesAsync(
            targetLeagueIds.Count > 0 ? targetLeagueIds : null,
            cancellationToken);

        var result = new LiveFixtureStatusSyncResultDto
        {
            ScopedToActiveSupportedLeagues = leagueId is null && activeOnly,
            TargetLeagueCount = targetLeagueIds.Count,
            LiveFixturesReceived = liveFixtures.Count,
            RequestsUsed = 1,
            ExecutedAtUtc = nowUtc
        };
        var touchedScopes = new HashSet<string>(StringComparer.Ordinal);

        if (liveFixtures.Count > 0)
        {
            var fixtureIds = liveFixtures.Select(x => x.Fixture.Id).Distinct().ToList();
            var teamApiIds = liveFixtures
                .SelectMany(x => new[] { x.Teams.Home.Id, x.Teams.Away.Id })
                .Distinct()
                .ToList();
            var leagueSeasonKeys = liveFixtures
                .Select(x => new { x.League.Id, x.League.Season })
                .Distinct()
                .ToList();

            var leagueIds = leagueSeasonKeys.Select(x => x.Id).Distinct().ToList();
            var seasons = leagueSeasonKeys.Select(x => x.Season).Distinct().ToList();

            var leagues = await _dbContext.Leagues
                .AsNoTracking()
                .Where(x => leagueIds.Contains(x.ApiLeagueId) && seasons.Contains(x.Season))
                .ToListAsync(cancellationToken);

            var teams = await _dbContext.Teams
                .AsNoTracking()
                .Where(x => teamApiIds.Contains(x.ApiTeamId))
                .ToListAsync(cancellationToken);

            var existingFixtures = await _dbContext.Fixtures
                .Where(x => fixtureIds.Contains(x.ApiFixtureId))
                .ToListAsync(cancellationToken);

            var leaguesByKey = leagues.ToDictionary(x => BuildLeagueKey(x.ApiLeagueId, x.Season));
            var teamsByApiId = teams.ToDictionary(x => x.ApiTeamId);
            var fixturesByApiId = existingFixtures.ToDictionary(x => x.ApiFixtureId);

            foreach (var item in liveFixtures.OrderBy(x => x.Fixture.Date))
            {
                if (!leaguesByKey.TryGetValue(BuildLeagueKey(item.League.Id, item.League.Season), out var league))
                {
                    result.FixturesSkippedMissingLeague++;
                    continue;
                }

                if (!teamsByApiId.TryGetValue(item.Teams.Home.Id, out var homeTeam) ||
                    !teamsByApiId.TryGetValue(item.Teams.Away.Id, out var awayTeam))
                {
                    result.FixturesSkippedMissingTeams++;
                    continue;
                }

                if (!fixturesByApiId.TryGetValue(item.Fixture.Id, out var fixture))
                {
                    fixture = new Fixture
                    {
                        ApiFixtureId = item.Fixture.Id
                    };

                    FixtureSyncService.ApplyFixtureData(
                        fixture,
                        league.Id,
                        league.Season,
                        homeTeam.Id,
                        awayTeam.Id,
                        item,
                        FixtureStatusMapper.NormalizeShort(item.Fixture.Status?.Short));

                    fixture.LastLiveStatusSyncedAtUtc = nowUtc;

                    _dbContext.Fixtures.Add(fixture);
                    fixturesByApiId[item.Fixture.Id] = fixture;
                    result.FixturesInserted++;
                    result.FixturesProcessed++;
                    touchedScopes.Add(BuildLeagueKey(league.ApiLeagueId, league.Season));
                    continue;
                }

                var isChanged = FixtureSyncService.ApplyFixtureData(
                    fixture,
                    league.Id,
                    league.Season,
                    homeTeam.Id,
                    awayTeam.Id,
                    item,
                    FixtureStatusMapper.NormalizeShort(item.Fixture.Status?.Short));

                if (fixture.LastLiveStatusSyncedAtUtc != nowUtc)
                {
                    fixture.LastLiveStatusSyncedAtUtc = nowUtc;
                }

                if (isChanged)
                {
                    result.FixturesUpdated++;
                }
                else
                {
                    result.FixturesUnchanged++;
                }

                result.FixturesProcessed++;
                touchedScopes.Add(BuildLeagueKey(league.ApiLeagueId, league.Season));
            }
        }

        var catchUpCandidates = await LoadCatchUpCandidatesAsync(
            targetLeagueIds,
            liveFixtures.Select(x => x.Fixture.Id).ToHashSet(),
            nowUtc,
            cancellationToken);

        result.CatchUpFixturesRequested = catchUpCandidates.Count;

        if (catchUpCandidates.Count > 0)
        {
            var catchUpFixtureIds = catchUpCandidates
                .Select(x => x.ApiFixtureId)
                .ToList();

            var catchUpItems = await _apiService.GetFixturesByIdsAsync(
                catchUpFixtureIds,
                cancellationToken);

            result.RequestsUsed++;
            result.CatchUpFixturesReceived = catchUpItems.Count;

            if (catchUpItems.Count > 0)
            {
                var existingCatchUpFixtures = await _dbContext.Fixtures
                    .Where(x => catchUpFixtureIds.Contains(x.ApiFixtureId))
                    .ToListAsync(cancellationToken);

                var existingCatchUpByApiId = existingCatchUpFixtures.ToDictionary(x => x.ApiFixtureId);

                foreach (var item in catchUpItems.OrderBy(x => x.Fixture.Date))
                {
                    if (!existingCatchUpByApiId.TryGetValue(item.Fixture.Id, out var fixture))
                        continue;

                    var isChanged = FixtureSyncService.ApplyFixtureData(
                        fixture,
                        fixture.LeagueId,
                        fixture.Season,
                        fixture.HomeTeamId,
                        fixture.AwayTeamId,
                        item,
                        FixtureStatusMapper.NormalizeShort(item.Fixture.Status?.Short));

                    fixture.LastLiveStatusSyncedAtUtc = nowUtc;

                    if (isChanged)
                    {
                        result.FixturesUpdated++;
                    }
                    else
                    {
                        result.FixturesUnchanged++;
                    }

                    result.FixturesProcessed++;
                    touchedScopes.Add(BuildLeagueKey(item.League.Id, item.League.Season));
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
                EntityType = "fixtures_live",
                LeagueApiId = x!.LeagueApiId,
                Season = x.Season,
                SyncedAtUtc = nowUtc
            })
            .ToList();

        await _syncStateService.SetLastSyncedAtBatchAsync(syncStateItems, cancellationToken);

        return result;
    }

    private async Task<List<CatchUpFixtureCandidate>> LoadCatchUpCandidatesAsync(
        IReadOnlyCollection<long> targetLeagueIds,
        IReadOnlySet<long> liveFixtureIds,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var catchUpWindowStart = nowUtc.Add(-CatchUpLookbackWindow);
        var catchUpWindowEnd = nowUtc.Add(CatchUpLookaheadWindow);
        var staleSyncThreshold = nowUtc.Add(-EndgameCatchUpMinResyncInterval);
        var endgameElapsedMinutes = _automationOptionsMonitor.CurrentValue.GetLiveStatusEndgameElapsedMinutes();

        var query = _dbContext.Fixtures
            .AsNoTracking()
            .Where(x =>
                x.Status != null &&
                (UpcomingStatuses.Contains(x.Status) || LiveStatuses.Contains(x.Status)) &&
                x.KickoffAt >= catchUpWindowStart &&
                x.KickoffAt <= catchUpWindowEnd &&
                (!x.LastLiveStatusSyncedAtUtc.HasValue || x.LastLiveStatusSyncedAtUtc <= staleSyncThreshold));

        if (targetLeagueIds.Count > 0)
        {
            query = query.Where(x => targetLeagueIds.Contains(x.League.ApiLeagueId));
        }

        var candidates = await query
            .Select(x => new CatchUpFixtureCandidate
            {
                ApiFixtureId = x.ApiFixtureId,
                KickoffAtUtc = x.KickoffAt,
                LastLiveStatusSyncedAtUtc = x.LastLiveStatusSyncedAtUtc,
                Status = x.Status!,
                Elapsed = x.Elapsed
            })
            .ToListAsync(cancellationToken);

        return candidates
            .Where(x => !liveFixtureIds.Contains(x.ApiFixtureId))
            .Where(x => !x.LastLiveStatusSyncedAtUtc.HasValue ||
                        x.LastLiveStatusSyncedAtUtc <= nowUtc - ResolveCatchUpInterval(x, nowUtc, endgameElapsedMinutes))
            .OrderByDescending(x => IsEndgameCandidate(x, nowUtc, endgameElapsedMinutes))
            .ThenBy(x => x.LastLiveStatusSyncedAtUtc ?? DateTime.MinValue)
            .ThenByDescending(x => x.KickoffAtUtc)
            .Take(CatchUpBatchSize)
            .ToList();
    }

    private async Task<List<long>> ResolveTargetLeagueIdsAsync(
        long? leagueId,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        if (leagueId.HasValue)
            return new List<long> { leagueId.Value };

        if (!activeOnly)
            return new List<long>();

        return await _dbContext.SupportedLeagues
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.LeagueApiId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    private static string BuildLeagueKey(long leagueApiId, int season)
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

    private sealed record LeagueSeasonScope(long LeagueApiId, int Season);

    private sealed class CatchUpFixtureCandidate
    {
        public long ApiFixtureId { get; set; }
        public DateTime KickoffAtUtc { get; set; }
        public DateTime? LastLiveStatusSyncedAtUtc { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? Elapsed { get; set; }
    }

    private static TimeSpan ResolveCatchUpInterval(
        CatchUpFixtureCandidate candidate,
        DateTime nowUtc,
        int endgameElapsedMinutes)
    {
        return IsEndgameCandidate(candidate, nowUtc, endgameElapsedMinutes)
            ? EndgameCatchUpMinResyncInterval
            : CatchUpMinResyncInterval;
    }

    private static bool IsEndgameCandidate(
        CatchUpFixtureCandidate candidate,
        DateTime nowUtc,
        int endgameElapsedMinutes)
    {
        if (!LiveStatuses.Contains(candidate.Status, StringComparer.OrdinalIgnoreCase))
            return false;

        if (candidate.Elapsed.HasValue && candidate.Elapsed.Value >= endgameElapsedMinutes)
            return true;

        return nowUtc - candidate.KickoffAtUtc >= TimeSpan.FromMinutes(endgameElapsedMinutes + 15);
    }
}
