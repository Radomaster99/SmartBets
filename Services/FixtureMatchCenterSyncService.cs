using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Entities;
using SmartBets.Enums;
using SmartBets.Models.ApiFootball;

namespace SmartBets.Services;

public class FixtureMatchCenterSyncService
{
    private static readonly TimeSpan LiveEventsInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LiveStatisticsInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LivePlayersInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan UpcomingLineupsInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PostFinishInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RecentFinishedWindow = TimeSpan.FromHours(3);
    private static readonly TimeSpan UpcomingLineupsWindow = TimeSpan.FromMinutes(45);
    private const int BatchFixtureIdsSize = 10;
    private const int MaxPostFinishRefreshes = 2;

    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _apiService;
    private readonly LeagueCoverageService _leagueCoverageService;
    private readonly SyncStateService _syncStateService;

    public FixtureMatchCenterSyncService(
        AppDbContext dbContext,
        FootballApiService apiService,
        LeagueCoverageService leagueCoverageService,
        SyncStateService syncStateService)
    {
        _dbContext = dbContext;
        _apiService = apiService;
        _leagueCoverageService = leagueCoverageService;
        _syncStateService = syncStateService;
    }

    public async Task<FixtureMatchCenterSyncDto> SyncFixtureAsync(
        long apiFixtureId,
        bool includePlayers = true,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var fixture = await _dbContext.Fixtures
            .Include(x => x.League)
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            .FirstOrDefaultAsync(x => x.ApiFixtureId == apiFixtureId, cancellationToken);

        if (fixture is null)
            throw new InvalidOperationException($"Fixture {apiFixtureId} was not found in database.");

        var coverage = await _leagueCoverageService.GetCoverageAsync(
            fixture.League.ApiLeagueId,
            fixture.Season,
            cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var bucket = FixtureStatusMapper.GetStateBucket(fixture.Status);
        var skippedComponents = new List<string>();

        var shouldSyncEvents = ShouldSyncEvents(fixture, bucket, coverage, force, nowUtc, skippedComponents);
        var shouldSyncStatistics = ShouldSyncStatistics(fixture, bucket, coverage, force, nowUtc, skippedComponents);
        var shouldSyncLineups = ShouldSyncLineups(fixture, bucket, coverage, force, nowUtc, skippedComponents);
        var shouldSyncPlayers = ShouldSyncPlayers(fixture, bucket, coverage, includePlayers, force, nowUtc, skippedComponents);

        if (bucket != FixtureStateBucket.Finished && fixture.PostFinishMatchCenterSyncCount != 0)
        {
            fixture.PostFinishMatchCenterSyncCount = 0;
        }

        if (shouldSyncEvents)
        {
            var events = await _apiService.GetFixtureEventsAsync(apiFixtureId, cancellationToken);
            await ReplaceEventsAsync(fixture, events, nowUtc, cancellationToken);
            fixture.LastEventSyncedAtUtc = nowUtc;

            await _syncStateService.SetLastSyncedAtAsync(
                "fixture_events",
                fixture.League.ApiLeagueId,
                fixture.Season,
                nowUtc,
                cancellationToken);
        }

        if (shouldSyncStatistics)
        {
            var statistics = await _apiService.GetFixtureStatisticsAsync(apiFixtureId, cancellationToken);
            await ReplaceStatisticsAsync(fixture, statistics, nowUtc, cancellationToken);
            fixture.LastStatisticsSyncedAtUtc = nowUtc;

            await _syncStateService.SetLastSyncedAtAsync(
                "fixture_statistics",
                fixture.League.ApiLeagueId,
                fixture.Season,
                nowUtc,
                cancellationToken);
        }

        if (shouldSyncLineups)
        {
            var lineups = await _apiService.GetFixtureLineupsAsync(apiFixtureId, cancellationToken);
            await ReplaceLineupsAsync(fixture, lineups, nowUtc, cancellationToken);
            fixture.LastLineupsSyncedAtUtc = nowUtc;

            await _syncStateService.SetLastSyncedAtAsync(
                "fixture_lineups",
                fixture.League.ApiLeagueId,
                fixture.Season,
                nowUtc,
                cancellationToken);
        }

        if (shouldSyncPlayers)
        {
            var players = await _apiService.GetFixturePlayersAsync(apiFixtureId, cancellationToken);
            await ReplacePlayerStatisticsAsync(fixture, players, nowUtc, cancellationToken);
            fixture.LastPlayerStatisticsSyncedAtUtc = nowUtc;

            await _syncStateService.SetLastSyncedAtAsync(
                "fixture_player_statistics",
                fixture.League.ApiLeagueId,
                fixture.Season,
                nowUtc,
                cancellationToken);
        }

        if (bucket == FixtureStateBucket.Finished &&
            (shouldSyncEvents || shouldSyncStatistics || shouldSyncLineups || shouldSyncPlayers))
        {
            fixture.PostFinishMatchCenterSyncCount++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new FixtureMatchCenterSyncDto
        {
            ApiFixtureId = fixture.ApiFixtureId,
            LeagueApiId = fixture.League.ApiLeagueId,
            Season = fixture.Season,
            StateBucket = bucket.ToString(),
            Forced = force,
            PlayersIncluded = includePlayers,
            EventsSynced = shouldSyncEvents,
            StatisticsSynced = shouldSyncStatistics,
            LineupsSynced = shouldSyncLineups,
            PlayersSynced = shouldSyncPlayers,
            SkippedComponents = skippedComponents,
            ExecutedAtUtc = nowUtc,
            Freshness = MapFreshness(fixture)
        };
    }

    public async Task<FixtureCornersSyncDto> SyncFixtureStatisticsAsync(
        long apiFixtureId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var fixture = await _dbContext.Fixtures
            .Include(x => x.League)
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            .FirstOrDefaultAsync(x => x.ApiFixtureId == apiFixtureId, cancellationToken);

        if (fixture is null)
            throw new InvalidOperationException($"Fixture {apiFixtureId} was not found in database.");

        var coverage = await _leagueCoverageService.GetCoverageAsync(
            fixture.League.ApiLeagueId,
            fixture.Season,
            cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var bucket = FixtureStatusMapper.GetStateBucket(fixture.Status);
        var skippedComponents = new List<string>();

        var shouldSyncStatistics = ShouldSyncStatistics(
            fixture,
            bucket,
            coverage,
            force,
            nowUtc,
            skippedComponents);

        if (bucket != FixtureStateBucket.Finished && fixture.PostFinishMatchCenterSyncCount != 0)
        {
            fixture.PostFinishMatchCenterSyncCount = 0;
        }

        if (shouldSyncStatistics)
        {
            var statistics = await _apiService.GetFixtureStatisticsAsync(apiFixtureId, cancellationToken);
            await ReplaceStatisticsAsync(fixture, statistics, nowUtc, cancellationToken);
            fixture.LastStatisticsSyncedAtUtc = nowUtc;

            await _syncStateService.SetLastSyncedAtAsync(
                "fixture_statistics",
                fixture.League.ApiLeagueId,
                fixture.Season,
                nowUtc,
                cancellationToken);

            if (bucket == FixtureStateBucket.Finished)
            {
                fixture.PostFinishMatchCenterSyncCount++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new FixtureCornersSyncDto
        {
            ApiFixtureId = fixture.ApiFixtureId,
            LeagueApiId = fixture.League.ApiLeagueId,
            Season = fixture.Season,
            StateBucket = bucket.ToString(),
            Forced = force,
            StatisticsSynced = shouldSyncStatistics,
            SkippedComponents = skippedComponents,
            ExecutedAtUtc = nowUtc,
            Freshness = MapFreshness(fixture)
        };
    }

    public async Task<LiveMatchCenterSyncDto> SyncLiveFixturesAsync(
        long? leagueId = null,
        int? season = null,
        int maxFixtures = 10,
        bool includePlayers = false,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        maxFixtures = Math.Clamp(maxFixtures, 1, 20);
        var nowUtc = DateTime.UtcNow;
        var liveStatuses = FixtureStatusMapper.GetStatusesForBucket(FixtureStateBucket.Live).ToArray();
        var finishedStatuses = FixtureStatusMapper.GetStatusesForBucket(FixtureStateBucket.Finished).ToArray();

        var query = _dbContext.Fixtures
            .Where(x =>
                (x.Status != null && liveStatuses.Contains(x.Status)) ||
                (x.Status != null &&
                 finishedStatuses.Contains(x.Status) &&
                 x.PostFinishMatchCenterSyncCount < MaxPostFinishRefreshes &&
                 x.KickoffAt >= nowUtc - RecentFinishedWindow))
            .Include(x => x.League)
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            .AsQueryable();

        if (leagueId.HasValue)
        {
            query = query.Where(x => x.League.ApiLeagueId == leagueId.Value);
        }

        if (season.HasValue)
        {
            query = query.Where(x => x.Season == season.Value);
        }

        var fixtures = await query
            .OrderBy(x => liveStatuses.Contains(x.Status! ) ? 0 : 1)
            .ThenBy(x => x.KickoffAt)
            .Take(maxFixtures)
            .ToListAsync(cancellationToken);

        var coverageCache = new Dictionary<string, LeagueSeasonCoverage?>(StringComparer.Ordinal);
        var plans = new List<FixtureBatchSyncPlan>();

        foreach (var fixture in fixtures)
        {
            var coverageKey = $"{fixture.League.ApiLeagueId}:{fixture.Season}";
            if (!coverageCache.TryGetValue(coverageKey, out var coverage))
            {
                coverage = await _leagueCoverageService.GetCoverageAsync(
                    fixture.League.ApiLeagueId,
                    fixture.Season,
                    cancellationToken);

                coverageCache[coverageKey] = coverage;
            }

            var bucket = FixtureStatusMapper.GetStateBucket(fixture.Status);
            var skippedComponents = new List<string>();

            var shouldSyncEvents = ShouldSyncEvents(fixture, bucket, coverage, force, nowUtc, skippedComponents);
            var shouldSyncStatistics = ShouldSyncStatistics(fixture, bucket, coverage, force, nowUtc, skippedComponents);
            var shouldSyncLineups = ShouldSyncLineups(fixture, bucket, coverage, force, nowUtc, skippedComponents);
            var shouldSyncPlayers = ShouldSyncPlayers(fixture, bucket, coverage, includePlayers, force, nowUtc, skippedComponents);

            if (bucket != FixtureStateBucket.Finished && fixture.PostFinishMatchCenterSyncCount != 0)
            {
                fixture.PostFinishMatchCenterSyncCount = 0;
            }

            plans.Add(new FixtureBatchSyncPlan
            {
                Fixture = fixture,
                Bucket = bucket,
                SkippedComponents = skippedComponents,
                ShouldSyncEvents = shouldSyncEvents,
                ShouldSyncStatistics = shouldSyncStatistics,
                ShouldSyncLineups = shouldSyncLineups,
                ShouldSyncPlayers = shouldSyncPlayers
            });
        }

        var fixturesToFetch = plans
            .Where(x => x.ShouldSyncEvents || x.ShouldSyncStatistics || x.ShouldSyncLineups || x.ShouldSyncPlayers)
            .Select(x => x.Fixture.ApiFixtureId)
            .Distinct()
            .ToList();

        var fixturesByApiId = new Dictionary<long, ApiFootballFixtureItem>();

        foreach (var chunk in fixturesToFetch.Chunk(BatchFixtureIdsSize))
        {
            var chunkItems = await _apiService.GetFixturesByIdsAsync(chunk, cancellationToken);
            foreach (var item in chunkItems)
            {
                fixturesByApiId[item.Fixture.Id] = item;
            }
        }

        var items = new List<FixtureMatchCenterSyncDto>();
        var syncStateItems = new List<SyncStateUpsertItem>();

        foreach (var plan in plans)
        {
            var fixture = plan.Fixture;
            var syncedAny = false;
            fixturesByApiId.TryGetValue(fixture.ApiFixtureId, out var source);

            if ((plan.ShouldSyncEvents || plan.ShouldSyncStatistics || plan.ShouldSyncLineups || plan.ShouldSyncPlayers) &&
                source is null)
            {
                plan.SkippedComponents.Add("batch:not_returned");
                items.Add(new FixtureMatchCenterSyncDto
                {
                    ApiFixtureId = fixture.ApiFixtureId,
                    LeagueApiId = fixture.League.ApiLeagueId,
                    Season = fixture.Season,
                    StateBucket = plan.Bucket.ToString(),
                    Forced = force,
                    PlayersIncluded = includePlayers,
                    EventsSynced = false,
                    StatisticsSynced = false,
                    LineupsSynced = false,
                    PlayersSynced = false,
                    SkippedComponents = plan.SkippedComponents,
                    ExecutedAtUtc = nowUtc,
                    Freshness = MapFreshness(fixture)
                });
                continue;
            }

            if (plan.ShouldSyncEvents)
            {
                await ReplaceEventsAsync(fixture, source!.Events, nowUtc, cancellationToken);
                fixture.LastEventSyncedAtUtc = nowUtc;
                syncedAny = true;
                syncStateItems.Add(new SyncStateUpsertItem
                {
                    EntityType = "fixture_events",
                    LeagueApiId = fixture.League.ApiLeagueId,
                    Season = fixture.Season,
                    SyncedAtUtc = nowUtc
                });
            }

            if (plan.ShouldSyncStatistics)
            {
                await ReplaceStatisticsAsync(fixture, source!.Statistics, nowUtc, cancellationToken);
                fixture.LastStatisticsSyncedAtUtc = nowUtc;
                syncedAny = true;
                syncStateItems.Add(new SyncStateUpsertItem
                {
                    EntityType = "fixture_statistics",
                    LeagueApiId = fixture.League.ApiLeagueId,
                    Season = fixture.Season,
                    SyncedAtUtc = nowUtc
                });
            }

            if (plan.ShouldSyncLineups)
            {
                await ReplaceLineupsAsync(fixture, source!.Lineups, nowUtc, cancellationToken);
                fixture.LastLineupsSyncedAtUtc = nowUtc;
                syncedAny = true;
                syncStateItems.Add(new SyncStateUpsertItem
                {
                    EntityType = "fixture_lineups",
                    LeagueApiId = fixture.League.ApiLeagueId,
                    Season = fixture.Season,
                    SyncedAtUtc = nowUtc
                });
            }

            if (plan.ShouldSyncPlayers)
            {
                await ReplacePlayerStatisticsAsync(fixture, source!.Players, nowUtc, cancellationToken);
                fixture.LastPlayerStatisticsSyncedAtUtc = nowUtc;
                syncedAny = true;
                syncStateItems.Add(new SyncStateUpsertItem
                {
                    EntityType = "fixture_player_statistics",
                    LeagueApiId = fixture.League.ApiLeagueId,
                    Season = fixture.Season,
                    SyncedAtUtc = nowUtc
                });
            }

            if (plan.Bucket == FixtureStateBucket.Finished && syncedAny)
            {
                fixture.PostFinishMatchCenterSyncCount++;
            }

            items.Add(new FixtureMatchCenterSyncDto
            {
                ApiFixtureId = fixture.ApiFixtureId,
                LeagueApiId = fixture.League.ApiLeagueId,
                Season = fixture.Season,
                StateBucket = plan.Bucket.ToString(),
                Forced = force,
                PlayersIncluded = includePlayers,
                EventsSynced = plan.ShouldSyncEvents,
                StatisticsSynced = plan.ShouldSyncStatistics,
                LineupsSynced = plan.ShouldSyncLineups,
                PlayersSynced = plan.ShouldSyncPlayers,
                SkippedComponents = plan.SkippedComponents,
                ExecutedAtUtc = nowUtc,
                Freshness = MapFreshness(fixture)
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _syncStateService.SetLastSyncedAtBatchAsync(syncStateItems, cancellationToken);

        return new LiveMatchCenterSyncDto
        {
            FixturesConsidered = fixtures.Count,
            FixturesSynced = items.Count(x => x.EventsSynced || x.StatisticsSynced || x.LineupsSynced || x.PlayersSynced),
            PlayersIncluded = includePlayers,
            ExecutedAtUtc = nowUtc,
            Items = items
        };
    }

    private async Task ReplaceEventsAsync(
        Fixture fixture,
        List<ApiFootballFixtureEventItem> source,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        await _dbContext.FixtureEvents
            .Where(x => x.FixtureId == fixture.Id)
            .ExecuteDeleteAsync(cancellationToken);

        var eventRows = source
            .Select((item, index) => new FixtureEvent
            {
                FixtureId = fixture.Id,
                SortOrder = index,
                Elapsed = item.Time.Elapsed,
                Extra = item.Time.Extra,
                TeamId = ResolveTeamId(fixture, item.Team.Id),
                ApiTeamId = item.Team.Id,
                TeamName = item.Team.Name ?? string.Empty,
                TeamLogoUrl = item.Team.Logo,
                PlayerApiId = item.Player.Id,
                PlayerName = item.Player.Name,
                AssistApiId = item.Assist.Id,
                AssistName = item.Assist.Name,
                Type = item.Type,
                Detail = item.Detail,
                Comments = item.Comments,
                SyncedAtUtc = syncedAtUtc
            })
            .ToList();

        if (eventRows.Count > 0)
        {
            _dbContext.FixtureEvents.AddRange(eventRows);
        }
    }

    private async Task ReplaceLineupsAsync(
        Fixture fixture,
        List<ApiFootballFixtureLineupItem> source,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        await _dbContext.FixtureLineups
            .Where(x => x.FixtureId == fixture.Id)
            .ExecuteDeleteAsync(cancellationToken);

        var rows = new List<FixtureLineup>();

        foreach (var teamLineup in source)
        {
            var resolvedTeamId = ResolveTeamId(fixture, teamLineup.Team.Id);

            rows.AddRange(teamLineup.StartXI.Select((item, index) => MapLineup(
                fixture.Id,
                resolvedTeamId,
                teamLineup,
                item.Player,
                true,
                index,
                syncedAtUtc)));

            rows.AddRange(teamLineup.Substitutes.Select((item, index) => MapLineup(
                fixture.Id,
                resolvedTeamId,
                teamLineup,
                item.Player,
                false,
                index,
                syncedAtUtc)));
        }

        if (rows.Count > 0)
        {
            _dbContext.FixtureLineups.AddRange(rows);
        }
    }

    private async Task ReplaceStatisticsAsync(
        Fixture fixture,
        List<ApiFootballFixtureStatisticsItem> source,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        await _dbContext.FixtureStatistics
            .Where(x => x.FixtureId == fixture.Id)
            .ExecuteDeleteAsync(cancellationToken);

        var rows = new List<FixtureStatistic>();

        foreach (var teamStats in source)
        {
            var resolvedTeamId = ResolveTeamId(fixture, teamStats.Team.Id);

            rows.AddRange(teamStats.Statistics.Select((item, index) => new FixtureStatistic
            {
                FixtureId = fixture.Id,
                TeamId = resolvedTeamId,
                ApiTeamId = teamStats.Team.Id,
                TeamName = teamStats.Team.Name,
                TeamLogoUrl = teamStats.Team.Logo,
                SortOrder = index,
                Type = item.Type,
                Value = item.Value,
                SyncedAtUtc = syncedAtUtc
            }));
        }

        if (rows.Count > 0)
        {
            _dbContext.FixtureStatistics.AddRange(rows);
        }
    }

    private async Task ReplacePlayerStatisticsAsync(
        Fixture fixture,
        List<ApiFootballFixturePlayersTeamItem> source,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        await _dbContext.FixturePlayerStatistics
            .Where(x => x.FixtureId == fixture.Id)
            .ExecuteDeleteAsync(cancellationToken);

        var rows = new List<FixturePlayerStatistic>();

        foreach (var teamPlayers in source)
        {
            var resolvedTeamId = ResolveTeamId(fixture, teamPlayers.Team.Id);

            foreach (var playerItem in teamPlayers.Players.Select((value, index) => new { value, index }))
            {
                var stats = playerItem.value.Statistics.FirstOrDefault() ?? new ApiFootballFixturePlayerStatisticsBlock();

                rows.Add(new FixturePlayerStatistic
                {
                    FixtureId = fixture.Id,
                    TeamId = resolvedTeamId,
                    ApiTeamId = teamPlayers.Team.Id,
                    TeamName = teamPlayers.Team.Name,
                    TeamLogoUrl = teamPlayers.Team.Logo,
                    SortOrder = playerItem.index,
                    PlayerApiId = playerItem.value.Player.Id,
                    PlayerName = playerItem.value.Player.Name,
                    PlayerPhotoUrl = playerItem.value.Player.Photo,
                    Minutes = stats.Games.Minutes,
                    Number = stats.Games.Number,
                    Position = stats.Games.Position,
                    Rating = ParseDecimal(stats.Games.Rating),
                    IsCaptain = stats.Games.Captain ?? false,
                    IsSubstitute = stats.Games.Substitute ?? false,
                    Offsides = stats.Offsides,
                    ShotsTotal = stats.Shots.Total,
                    ShotsOn = stats.Shots.On,
                    GoalsTotal = stats.Goals.Total,
                    GoalsConceded = stats.Goals.Conceded,
                    GoalsAssists = stats.Goals.Assists,
                    GoalsSaves = stats.Goals.Saves,
                    PassesTotal = stats.Passes.Total,
                    PassesKey = stats.Passes.Key,
                    PassesAccuracy = stats.Passes.Accuracy,
                    TacklesTotal = stats.Tackles.Total,
                    TacklesBlocks = stats.Tackles.Blocks,
                    TacklesInterceptions = stats.Tackles.Interceptions,
                    DuelsTotal = stats.Duels.Total,
                    DuelsWon = stats.Duels.Won,
                    DribblesAttempts = stats.Dribbles.Attempts,
                    DribblesSuccess = stats.Dribbles.Success,
                    DribblesPast = stats.Dribbles.Past,
                    FoulsDrawn = stats.Fouls.Drawn,
                    FoulsCommitted = stats.Fouls.Committed,
                    CardsYellow = stats.Cards.Yellow,
                    CardsRed = stats.Cards.Red,
                    PenaltyWon = stats.Penalty.Won,
                    PenaltyCommitted = stats.Penalty.Committed,
                    PenaltyScored = stats.Penalty.Scored,
                    PenaltyMissed = stats.Penalty.Missed,
                    PenaltySaved = stats.Penalty.Saved,
                    SyncedAtUtc = syncedAtUtc
                });
            }
        }

        if (rows.Count > 0)
        {
            _dbContext.FixturePlayerStatistics.AddRange(rows);
        }
    }

    private static FixtureLineup MapLineup(
        long fixtureId,
        long? teamId,
        ApiFootballFixtureLineupItem lineup,
        ApiFootballFixtureLineupPlayer player,
        bool isStarting,
        int sortOrder,
        DateTime syncedAtUtc)
    {
        return new FixtureLineup
        {
            FixtureId = fixtureId,
            TeamId = teamId,
            ApiTeamId = lineup.Team.Id,
            TeamName = lineup.Team.Name,
            TeamLogoUrl = lineup.Team.Logo,
            Formation = lineup.Formation,
            CoachApiId = lineup.Coach.Id,
            CoachName = lineup.Coach.Name,
            CoachPhotoUrl = lineup.Coach.Photo,
            IsStarting = isStarting,
            SortOrder = sortOrder,
            PlayerApiId = player.Id,
            PlayerName = player.Name,
            PlayerNumber = player.Number,
            PlayerPosition = player.Position,
            PlayerGrid = player.Grid,
            SyncedAtUtc = syncedAtUtc
        };
    }

    private static FixtureFreshnessDto MapFreshness(Fixture fixture)
    {
        return new FixtureFreshnessDto
        {
            LastLiveStatusSyncedAtUtc = fixture.LastLiveStatusSyncedAtUtc,
            LastEventSyncedAtUtc = fixture.LastEventSyncedAtUtc,
            LastStatisticsSyncedAtUtc = fixture.LastStatisticsSyncedAtUtc,
            LastLineupsSyncedAtUtc = fixture.LastLineupsSyncedAtUtc,
            LastPlayerStatisticsSyncedAtUtc = fixture.LastPlayerStatisticsSyncedAtUtc,
            LastPredictionSyncedAtUtc = fixture.LastPredictionSyncedAtUtc,
            LastInjuriesSyncedAtUtc = fixture.LastInjuriesSyncedAtUtc
        };
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static long? ResolveTeamId(Fixture fixture, long? apiTeamId)
    {
        if (!apiTeamId.HasValue)
            return null;

        if (fixture.HomeTeam.ApiTeamId == apiTeamId.Value)
            return fixture.HomeTeamId;

        if (fixture.AwayTeam.ApiTeamId == apiTeamId.Value)
            return fixture.AwayTeamId;

        return null;
    }

    private static bool ShouldSyncEvents(
        Fixture fixture,
        FixtureStateBucket bucket,
        LeagueSeasonCoverage? coverage,
        bool force,
        DateTime nowUtc,
        List<string> skipped)
    {
        if (coverage is not null && !coverage.HasFixtureEvents)
        {
            skipped.Add("events:coverage_disabled");
            return false;
        }

        if (force)
            return true;

        return bucket switch
        {
            FixtureStateBucket.Live => ShouldSyncByInterval(fixture.LastEventSyncedAtUtc, nowUtc, LiveEventsInterval, skipped, "events:throttled"),
            FixtureStateBucket.Finished => ShouldSyncFinished(fixture, fixture.LastEventSyncedAtUtc, nowUtc, skipped, "events:finished_limit"),
            _ => AddSkipped(skipped, "events:not_applicable")
        };
    }

    private static bool ShouldSyncStatistics(
        Fixture fixture,
        FixtureStateBucket bucket,
        LeagueSeasonCoverage? coverage,
        bool force,
        DateTime nowUtc,
        List<string> skipped)
    {
        if (coverage is not null && !coverage.HasFixtureStatistics)
        {
            skipped.Add("statistics:coverage_disabled");
            return false;
        }

        if (force)
            return true;

        return bucket switch
        {
            FixtureStateBucket.Live => ShouldSyncByInterval(fixture.LastStatisticsSyncedAtUtc, nowUtc, LiveStatisticsInterval, skipped, "statistics:throttled"),
            FixtureStateBucket.Finished => ShouldSyncFinished(fixture, fixture.LastStatisticsSyncedAtUtc, nowUtc, skipped, "statistics:finished_limit"),
            _ => AddSkipped(skipped, "statistics:not_applicable")
        };
    }

    private static bool ShouldSyncLineups(
        Fixture fixture,
        FixtureStateBucket bucket,
        LeagueSeasonCoverage? coverage,
        bool force,
        DateTime nowUtc,
        List<string> skipped)
    {
        if (coverage is not null && !coverage.HasLineups)
        {
            skipped.Add("lineups:coverage_disabled");
            return false;
        }

        if (force)
            return true;

        return bucket switch
        {
            FixtureStateBucket.Upcoming when nowUtc >= fixture.KickoffAt - UpcomingLineupsWindow =>
                ShouldSyncByInterval(fixture.LastLineupsSyncedAtUtc, nowUtc, UpcomingLineupsInterval, skipped, "lineups:throttled"),
            FixtureStateBucket.Live =>
                ShouldSyncByInterval(fixture.LastLineupsSyncedAtUtc, nowUtc, UpcomingLineupsInterval, skipped, "lineups:throttled"),
            FixtureStateBucket.Finished =>
                ShouldSyncFinished(fixture, fixture.LastLineupsSyncedAtUtc, nowUtc, skipped, "lineups:finished_limit"),
            FixtureStateBucket.Upcoming => AddSkipped(skipped, "lineups:too_early"),
            _ => AddSkipped(skipped, "lineups:not_applicable")
        };
    }

    private static bool ShouldSyncPlayers(
        Fixture fixture,
        FixtureStateBucket bucket,
        LeagueSeasonCoverage? coverage,
        bool includePlayers,
        bool force,
        DateTime nowUtc,
        List<string> skipped)
    {
        if (!includePlayers)
        {
            skipped.Add("players:not_requested");
            return false;
        }

        if (coverage is not null && !coverage.HasPlayerStatistics)
        {
            skipped.Add("players:coverage_disabled");
            return false;
        }

        if (force)
            return true;

        return bucket switch
        {
            FixtureStateBucket.Live => ShouldSyncByInterval(fixture.LastPlayerStatisticsSyncedAtUtc, nowUtc, LivePlayersInterval, skipped, "players:throttled"),
            FixtureStateBucket.Finished => ShouldSyncFinished(fixture, fixture.LastPlayerStatisticsSyncedAtUtc, nowUtc, skipped, "players:finished_limit"),
            _ => AddSkipped(skipped, "players:not_applicable")
        };
    }

    private static bool ShouldSyncByInterval(
        DateTime? lastSyncedAtUtc,
        DateTime nowUtc,
        TimeSpan interval,
        List<string> skipped,
        string skipReason)
    {
        if (!lastSyncedAtUtc.HasValue || nowUtc - lastSyncedAtUtc.Value >= interval)
            return true;

        skipped.Add(skipReason);
        return false;
    }

    private static bool ShouldSyncFinished(
        Fixture fixture,
        DateTime? lastSyncedAtUtc,
        DateTime nowUtc,
        List<string> skipped,
        string skipReason)
    {
        if (!lastSyncedAtUtc.HasValue)
            return true;

        if (fixture.PostFinishMatchCenterSyncCount >= MaxPostFinishRefreshes)
        {
            skipped.Add(skipReason);
            return false;
        }

        return ShouldSyncByInterval(lastSyncedAtUtc, nowUtc, PostFinishInterval, skipped, skipReason);
    }

    private static bool AddSkipped(List<string> skipped, string reason)
    {
        skipped.Add(reason);
        return false;
    }

    private sealed class FixtureBatchSyncPlan
    {
        public Fixture Fixture { get; set; } = null!;
        public FixtureStateBucket Bucket { get; set; }
        public List<string> SkippedComponents { get; set; } = new();
        public bool ShouldSyncEvents { get; set; }
        public bool ShouldSyncStatistics { get; set; }
        public bool ShouldSyncLineups { get; set; }
        public bool ShouldSyncPlayers { get; set; }
    }
}
