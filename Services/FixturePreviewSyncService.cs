using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Entities;
using SmartBets.Enums;
using SmartBets.Models.ApiFootball;

namespace SmartBets.Services;

public class FixturePreviewSyncService
{
    private static readonly TimeSpan InitialWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan Refresh3hWindow = TimeSpan.FromHours(3);
    private static readonly TimeSpan Refresh1hWindow = TimeSpan.FromHours(1);

    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _apiService;
    private readonly LeagueCoverageService _leagueCoverageService;
    private readonly SyncStateService _syncStateService;

    public FixturePreviewSyncService(
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

    public async Task<FixturePreviewSyncDto> SyncFixtureAsync(
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

        var nowUtc = DateTime.UtcNow;
        var stage = DetermineStage(fixture.KickoffAt, nowUtc, force);
        var bucket = FixtureStatusMapper.GetStateBucket(fixture.Status);
        var coverage = await _leagueCoverageService.GetCoverageAsync(
            fixture.League.ApiLeagueId,
            fixture.Season,
            cancellationToken);

        var skipped = new List<string>();
        var predictionSynced = false;
        var injuriesSynced = false;

        if (!force && bucket != FixtureStateBucket.Upcoming)
        {
            skipped.Add("preview:not_upcoming");
        }
        else
        {
            var predictionBoundary = GetStageBoundary(fixture.KickoffAt, stage);
            var injuriesBoundary = GetStageBoundary(fixture.KickoffAt, stage);

            if (coverage is not null && !coverage.HasPredictions)
            {
                skipped.Add("predictions:coverage_disabled");
            }
            else if (predictionBoundary is null && !force)
            {
                skipped.Add("predictions:outside_window");
            }
            else if (!force && fixture.LastPredictionSyncedAtUtc.HasValue && predictionBoundary.HasValue && fixture.LastPredictionSyncedAtUtc.Value >= predictionBoundary.Value)
            {
                skipped.Add("predictions:already_synced_for_stage");
            }
            else
            {
                var prediction = await _apiService.GetPredictionAsync(apiFixtureId, cancellationToken);
                await ReplacePredictionAsync(fixture, prediction, nowUtc, cancellationToken);
                fixture.LastPredictionSyncedAtUtc = nowUtc;
                predictionSynced = true;

                await _syncStateService.SetLastSyncedAtAsync(
                    "fixture_predictions",
                    fixture.League.ApiLeagueId,
                    fixture.Season,
                    nowUtc,
                    cancellationToken);
            }

            if (coverage is not null && !coverage.HasInjuries)
            {
                skipped.Add("injuries:coverage_disabled");
            }
            else if (injuriesBoundary is null && !force)
            {
                skipped.Add("injuries:outside_window");
            }
            else if (!force && fixture.LastInjuriesSyncedAtUtc.HasValue && injuriesBoundary.HasValue && fixture.LastInjuriesSyncedAtUtc.Value >= injuriesBoundary.Value)
            {
                skipped.Add("injuries:already_synced_for_stage");
            }
            else
            {
                var injuries = await _apiService.GetFixtureInjuriesAsync(apiFixtureId, cancellationToken);
                await ReplaceInjuriesAsync(fixture, injuries, nowUtc, cancellationToken);
                fixture.LastInjuriesSyncedAtUtc = nowUtc;
                injuriesSynced = true;

                await _syncStateService.SetLastSyncedAtAsync(
                    "fixture_injuries",
                    fixture.League.ApiLeagueId,
                    fixture.Season,
                    nowUtc,
                    cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new FixturePreviewSyncDto
        {
            ApiFixtureId = fixture.ApiFixtureId,
            LeagueApiId = fixture.League.ApiLeagueId,
            Season = fixture.Season,
            Stage = stage,
            Forced = force,
            PredictionSynced = predictionSynced,
            InjuriesSynced = injuriesSynced,
            SkippedComponents = skipped,
            ExecutedAtUtc = nowUtc,
            Freshness = MapFreshness(fixture)
        };
    }

    public async Task<UpcomingPreviewSyncDto> SyncUpcomingFixturesAsync(
        long? leagueId = null,
        int? season = null,
        int windowHours = 24,
        int maxFixtures = 10,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        windowHours = Math.Clamp(windowHours, 1, 48);
        maxFixtures = Math.Clamp(maxFixtures, 1, 25);

        var nowUtc = DateTime.UtcNow;
        var upcomingStatuses = FixtureStatusMapper.GetStatusesForBucket(FixtureStateBucket.Upcoming).ToArray();

        var query = _dbContext.Fixtures
            .AsNoTracking()
            .Include(x => x.League)
            .Where(x =>
                x.Status != null &&
                upcomingStatuses.Contains(x.Status) &&
                x.KickoffAt > nowUtc &&
                x.KickoffAt <= nowUtc.AddHours(windowHours))
            .AsQueryable();

        if (leagueId.HasValue)
        {
            query = query.Where(x => x.League.ApiLeagueId == leagueId.Value);
        }

        if (season.HasValue)
        {
            query = query.Where(x => x.Season == season.Value);
        }

        var fixtureIds = await query
            .OrderBy(x => x.KickoffAt)
            .Take(maxFixtures)
            .Select(x => x.ApiFixtureId)
            .ToListAsync(cancellationToken);

        var items = new List<FixturePreviewSyncDto>();

        foreach (var fixtureId in fixtureIds)
        {
            items.Add(await SyncFixtureAsync(fixtureId, force, cancellationToken));
        }

        return new UpcomingPreviewSyncDto
        {
            FixturesConsidered = fixtureIds.Count,
            FixturesSynced = items.Count(x => x.PredictionSynced || x.InjuriesSynced),
            WindowHours = windowHours,
            ExecutedAtUtc = nowUtc,
            Items = items
        };
    }

    private async Task ReplacePredictionAsync(
        Fixture fixture,
        ApiFootballPredictionItem? source,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        await _dbContext.FixturePredictions
            .Where(x => x.FixtureId == fixture.Id)
            .ExecuteDeleteAsync(cancellationToken);

        if (source is null)
            return;

        _dbContext.FixturePredictions.Add(new FixturePrediction
        {
            FixtureId = fixture.Id,
            WinnerTeamApiId = source.Predictions.Winner.Id,
            WinnerTeamName = source.Predictions.Winner.Name,
            WinnerComment = source.Predictions.Winner.Comment,
            WinOrDraw = ParseBool(source.Predictions.WinOrDraw),
            UnderOver = source.Predictions.UnderOver,
            Advice = source.Predictions.Advice,
            GoalsHome = source.Predictions.Goals.Home,
            GoalsAway = source.Predictions.Goals.Away,
            PercentHome = source.Predictions.Percent.Home,
            PercentDraw = source.Predictions.Percent.Draw,
            PercentAway = source.Predictions.Percent.Away,
            ComparisonFormHome = source.Comparison.Form.Home,
            ComparisonFormAway = source.Comparison.Form.Away,
            ComparisonAttackHome = source.Comparison.Attack.Home,
            ComparisonAttackAway = source.Comparison.Attack.Away,
            ComparisonDefenceHome = source.Comparison.Defence.Home,
            ComparisonDefenceAway = source.Comparison.Defence.Away,
            ComparisonPoissonHome = source.Comparison.PoissonDistribution.Home,
            ComparisonPoissonAway = source.Comparison.PoissonDistribution.Away,
            ComparisonHeadToHeadHome = source.Comparison.HeadToHead.Home,
            ComparisonHeadToHeadAway = source.Comparison.HeadToHead.Away,
            ComparisonGoalsHome = source.Comparison.Goals.Home,
            ComparisonGoalsAway = source.Comparison.Goals.Away,
            ComparisonTotalHome = source.Comparison.Total.Home,
            ComparisonTotalAway = source.Comparison.Total.Away,
            SyncedAtUtc = syncedAtUtc
        });
    }

    private async Task ReplaceInjuriesAsync(
        Fixture fixture,
        List<ApiFootballInjuryItem> source,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        await _dbContext.FixtureInjuries
            .Where(x => x.FixtureId == fixture.Id)
            .ExecuteDeleteAsync(cancellationToken);

        var rows = source.Select(item => new FixtureInjury
        {
            FixtureId = fixture.Id,
            TeamId = ResolveTeamId(fixture, item.Team.Id),
            ApiTeamId = item.Team.Id,
            TeamName = item.Team.Name ?? string.Empty,
            TeamLogoUrl = item.Team.Logo,
            PlayerApiId = item.Player.Id,
            PlayerName = item.Player.Name ?? string.Empty,
            PlayerPhotoUrl = item.Player.Photo,
            Type = item.Player.Type,
            Reason = item.Player.Reason,
            SyncedAtUtc = syncedAtUtc
        }).ToList();

        if (rows.Count > 0)
        {
            _dbContext.FixtureInjuries.AddRange(rows);
        }
    }

    private static string DetermineStage(DateTime kickoffAtUtc, DateTime nowUtc, bool force)
    {
        if (force)
            return "Forced";

        var timeToKickoff = kickoffAtUtc - nowUtc;

        if (timeToKickoff <= TimeSpan.Zero)
            return "Closed";

        if (timeToKickoff <= Refresh1hWindow)
            return "T-1h";

        if (timeToKickoff <= Refresh3hWindow)
            return "T-3h";

        if (timeToKickoff <= InitialWindow)
            return "T-24h";

        return "OutsideWindow";
    }

    private static DateTime? GetStageBoundary(DateTime kickoffAtUtc, string stage)
    {
        return stage switch
        {
            "Forced" => null,
            "T-24h" => kickoffAtUtc - InitialWindow,
            "T-3h" => kickoffAtUtc - Refresh3hWindow,
            "T-1h" => kickoffAtUtc - Refresh1hWindow,
            _ => null
        };
    }

    private static FixtureFreshnessDto MapFreshness(Fixture fixture)
    {
        return new FixtureFreshnessDto
        {
            LastEventSyncedAtUtc = fixture.LastEventSyncedAtUtc,
            LastStatisticsSyncedAtUtc = fixture.LastStatisticsSyncedAtUtc,
            LastLineupsSyncedAtUtc = fixture.LastLineupsSyncedAtUtc,
            LastPlayerStatisticsSyncedAtUtc = fixture.LastPlayerStatisticsSyncedAtUtc,
            LastPredictionSyncedAtUtc = fixture.LastPredictionSyncedAtUtc,
            LastInjuriesSyncedAtUtc = fixture.LastInjuriesSyncedAtUtc
        };
    }

    private static bool? ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return bool.TryParse(value, out var parsed)
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
}
