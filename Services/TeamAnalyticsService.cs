using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Entities;
using SmartBets.Enums;
using SmartBets.Models.ApiFootball;

namespace SmartBets.Services;

public class TeamAnalyticsService
{
    private static readonly TimeSpan DailyInterval = TimeSpan.FromHours(20);

    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _apiService;
    private readonly SyncStateService _syncStateService;

    public TeamAnalyticsService(
        AppDbContext dbContext,
        FootballApiService apiService,
        SyncStateService syncStateService)
    {
        _dbContext = dbContext;
        _apiService = apiService;
        _syncStateService = syncStateService;
    }

    public async Task<TeamStatisticsDto?> GetTeamStatisticsAsync(
        long apiTeamId,
        long leagueApiId,
        int season,
        CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.TeamStatistics
            .AsNoTracking()
            .Include(x => x.Team)
            .Include(x => x.League)
            .FirstOrDefaultAsync(
                x => x.Team.ApiTeamId == apiTeamId &&
                     x.League.ApiLeagueId == leagueApiId &&
                     x.Season == season,
                cancellationToken);

        return row is null ? null : MapStatistics(row);
    }

    public async Task<TeamRecentFormDto?> GetTeamFormAsync(
        long apiTeamId,
        long leagueApiId,
        int season,
        int last = 5,
        CancellationToken cancellationToken = default)
    {
        last = Math.Clamp(last, 1, 20);

        var team = await _dbContext.Teams
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ApiTeamId == apiTeamId, cancellationToken);

        var league = await _dbContext.Leagues
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ApiLeagueId == leagueApiId && x.Season == season, cancellationToken);

        if (team is null || league is null)
            return null;

        var finishedStatuses = FixtureStatusMapper.GetStatusesForBucket(FixtureStateBucket.Finished).ToArray();

        var fixtures = await _dbContext.Fixtures
            .AsNoTracking()
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            .Where(x =>
                x.LeagueId == league.Id &&
                x.Season == season &&
                x.Status != null &&
                finishedStatuses.Contains(x.Status) &&
                (x.HomeTeamId == team.Id || x.AwayTeamId == team.Id))
            .OrderByDescending(x => x.KickoffAt)
            .Take(last)
            .ToListAsync(cancellationToken);

        var items = fixtures
            .Select(x =>
            {
                var isHome = x.HomeTeamId == team.Id;
                var goalsFor = isHome ? x.HomeGoals : x.AwayGoals;
                var goalsAgainst = isHome ? x.AwayGoals : x.HomeGoals;

                return new TeamFormItemDto
                {
                    ApiFixtureId = x.ApiFixtureId,
                    KickoffAtUtc = x.KickoffAt,
                    OpponentName = isHome ? x.AwayTeam.Name : x.HomeTeam.Name,
                    OpponentLogoUrl = isHome ? x.AwayTeam.LogoUrl : x.HomeTeam.LogoUrl,
                    IsHome = isHome,
                    GoalsFor = goalsFor,
                    GoalsAgainst = goalsAgainst,
                    Result = CalculateResult(goalsFor, goalsAgainst)
                };
            })
            .ToList();

        return new TeamRecentFormDto
        {
            TeamApiId = team.ApiTeamId,
            TeamName = team.Name,
            TeamLogoUrl = team.LogoUrl,
            Form = string.Concat(items.Select(x => x.Result)),
            Wins = items.Count(x => x.Result == "W"),
            Draws = items.Count(x => x.Result == "D"),
            Losses = items.Count(x => x.Result == "L"),
            Fixtures = items
        };
    }

    public async Task<TeamStatisticsSyncResultDto> SyncStatisticsAsync(
        long leagueApiId,
        int season,
        long? apiTeamId = null,
        int maxTeams = 25,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        maxTeams = Math.Clamp(maxTeams, 1, 40);
        var nowUtc = DateTime.UtcNow;

        var league = await _dbContext.Leagues
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ApiLeagueId == leagueApiId && x.Season == season, cancellationToken);

        if (league is null)
            throw new InvalidOperationException($"League {leagueApiId} season {season} was not found.");

        var teams = await ResolveTeamsForLeagueAsync(league.Id, apiTeamId, maxTeams, cancellationToken);

        var existingStats = await _dbContext.TeamStatistics
            .Where(x => x.LeagueId == league.Id && x.Season == season)
            .ToListAsync(cancellationToken);

        var existingByTeamId = existingStats.ToDictionary(x => x.TeamId, x => x);
        var resultItems = new List<TeamStatisticsSyncItemDto>();
        var syncedCount = 0;
        var skippedFreshCount = 0;

        foreach (var team in teams)
        {
            if (existingByTeamId.TryGetValue(team.Id, out var existing) &&
                !force &&
                nowUtc - existing.SyncedAtUtc < DailyInterval)
            {
                skippedFreshCount++;
                resultItems.Add(new TeamStatisticsSyncItemDto
                {
                    ApiTeamId = team.ApiTeamId,
                    Synced = false,
                    Status = "SkippedFresh"
                });
                continue;
            }

            var apiStats = await _apiService.GetTeamStatisticsAsync(team.ApiTeamId, leagueApiId, season, cancellationToken);
            if (apiStats is null)
            {
                resultItems.Add(new TeamStatisticsSyncItemDto
                {
                    ApiTeamId = team.ApiTeamId,
                    Synced = false,
                    Status = "NoData"
                });
                continue;
            }

            if (existing is null)
            {
                existing = new TeamStatistic
                {
                    TeamId = team.Id,
                    LeagueId = league.Id,
                    Season = season
                };

                _dbContext.TeamStatistics.Add(existing);
                existingByTeamId[team.Id] = existing;
            }

            ApplyStatistics(existing, apiStats, nowUtc);
            syncedCount++;

            resultItems.Add(new TeamStatisticsSyncItemDto
            {
                ApiTeamId = team.ApiTeamId,
                Synced = true,
                Status = "Synced"
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (syncedCount > 0)
        {
            await _syncStateService.SetLastSyncedAtAsync(
                "team_statistics",
                leagueApiId,
                season,
                nowUtc,
                cancellationToken);
        }

        return new TeamStatisticsSyncResultDto
        {
            LeagueApiId = leagueApiId,
            Season = season,
            Forced = force,
            TeamsConsidered = teams.Count,
            TeamsSynced = syncedCount,
            TeamsSkippedFresh = skippedFreshCount,
            ExecutedAtUtc = nowUtc,
            Items = resultItems
        };
    }

    private async Task<List<Team>> ResolveTeamsForLeagueAsync(
        long leagueId,
        long? apiTeamId,
        int maxTeams,
        CancellationToken cancellationToken)
    {
        if (apiTeamId.HasValue)
        {
            var team = await _dbContext.Teams
                .FirstOrDefaultAsync(x => x.ApiTeamId == apiTeamId.Value, cancellationToken);

            return team is null ? new List<Team>() : new List<Team> { team };
        }

        var homeTeamIdsQuery = _dbContext.Fixtures
            .AsNoTracking()
            .Where(x => x.LeagueId == leagueId)
            .Select(x => x.HomeTeamId);

        var awayTeamIdsQuery = _dbContext.Fixtures
            .AsNoTracking()
            .Where(x => x.LeagueId == leagueId)
            .Select(x => x.AwayTeamId);

        var fixtureTeamIds = await homeTeamIdsQuery
            .Concat(awayTeamIdsQuery)
            .Distinct()
            .OrderBy(x => x)
            .Take(maxTeams)
            .ToListAsync(cancellationToken);

        if (fixtureTeamIds.Count > 0)
        {
            return await _dbContext.Teams
                .AsNoTracking()
                .Where(x => fixtureTeamIds.Contains(x.Id))
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);
        }

        var standingTeamIds = await _dbContext.Standings
            .AsNoTracking()
            .Where(x => x.LeagueId == leagueId)
            .OrderBy(x => x.Rank)
            .Select(x => x.TeamId)
            .Distinct()
            .Take(maxTeams)
            .ToListAsync(cancellationToken);

        return await _dbContext.Teams
            .AsNoTracking()
            .Where(x => standingTeamIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    private static void ApplyStatistics(TeamStatistic target, ApiFootballTeamStatisticsItem source, DateTime syncedAtUtc)
    {
        target.Form = source.Form ?? string.Empty;
        target.FixturesPlayedTotal = source.Fixtures.Played.Total;
        target.FixturesPlayedHome = source.Fixtures.Played.Home;
        target.FixturesPlayedAway = source.Fixtures.Played.Away;
        target.WinsTotal = source.Fixtures.Wins.Total;
        target.WinsHome = source.Fixtures.Wins.Home;
        target.WinsAway = source.Fixtures.Wins.Away;
        target.DrawsTotal = source.Fixtures.Draws.Total;
        target.DrawsHome = source.Fixtures.Draws.Home;
        target.DrawsAway = source.Fixtures.Draws.Away;
        target.LossesTotal = source.Fixtures.Loses.Total;
        target.LossesHome = source.Fixtures.Loses.Home;
        target.LossesAway = source.Fixtures.Loses.Away;
        target.GoalsForTotal = source.Goals.For.Total.Total;
        target.GoalsForHome = source.Goals.For.Total.Home;
        target.GoalsForAway = source.Goals.For.Total.Away;
        target.GoalsForAverageTotal = source.Goals.For.Average.Total;
        target.GoalsForAverageHome = source.Goals.For.Average.Home;
        target.GoalsForAverageAway = source.Goals.For.Average.Away;
        target.GoalsAgainstTotal = source.Goals.Against.Total.Total;
        target.GoalsAgainstHome = source.Goals.Against.Total.Home;
        target.GoalsAgainstAway = source.Goals.Against.Total.Away;
        target.GoalsAgainstAverageTotal = source.Goals.Against.Average.Total;
        target.GoalsAgainstAverageHome = source.Goals.Against.Average.Home;
        target.GoalsAgainstAverageAway = source.Goals.Against.Average.Away;
        target.CleanSheetsTotal = source.CleanSheet.Total;
        target.CleanSheetsHome = source.CleanSheet.Home;
        target.CleanSheetsAway = source.CleanSheet.Away;
        target.FailedToScoreTotal = source.FailedToScore.Total;
        target.FailedToScoreHome = source.FailedToScore.Home;
        target.FailedToScoreAway = source.FailedToScore.Away;
        target.BiggestStreakWins = source.Biggest.Streak.Wins;
        target.BiggestStreakDraws = source.Biggest.Streak.Draws;
        target.BiggestStreakLosses = source.Biggest.Streak.Loses;
        target.BiggestWinHome = source.Biggest.Wins.Home;
        target.BiggestWinAway = source.Biggest.Wins.Away;
        target.BiggestLossHome = source.Biggest.Loses.Home;
        target.BiggestLossAway = source.Biggest.Loses.Away;
        target.BiggestGoalsForHome = source.Biggest.Goals.For.Home;
        target.BiggestGoalsForAway = source.Biggest.Goals.For.Away;
        target.BiggestGoalsAgainstHome = source.Biggest.Goals.Against.Home;
        target.BiggestGoalsAgainstAway = source.Biggest.Goals.Against.Away;
        target.SyncedAtUtc = syncedAtUtc;
    }

    private static TeamStatisticsDto MapStatistics(TeamStatistic row)
    {
        return new TeamStatisticsDto
        {
            TeamApiId = row.Team.ApiTeamId,
            TeamName = row.Team.Name,
            TeamLogoUrl = row.Team.LogoUrl,
            LeagueApiId = row.League.ApiLeagueId,
            LeagueName = row.League.Name,
            Season = row.Season,
            Form = row.Form,
            FixturesPlayed = MapSummary(row.FixturesPlayedTotal, row.FixturesPlayedHome, row.FixturesPlayedAway),
            Wins = MapSummary(row.WinsTotal, row.WinsHome, row.WinsAway),
            Draws = MapSummary(row.DrawsTotal, row.DrawsHome, row.DrawsAway),
            Losses = MapSummary(row.LossesTotal, row.LossesHome, row.LossesAway),
            GoalsFor = MapSummary(row.GoalsForTotal, row.GoalsForHome, row.GoalsForAway),
            GoalsForAverage = new TeamStatisticsAverageDto
            {
                Total = row.GoalsForAverageTotal,
                Home = row.GoalsForAverageHome,
                Away = row.GoalsForAverageAway
            },
            GoalsAgainst = MapSummary(row.GoalsAgainstTotal, row.GoalsAgainstHome, row.GoalsAgainstAway),
            GoalsAgainstAverage = new TeamStatisticsAverageDto
            {
                Total = row.GoalsAgainstAverageTotal,
                Home = row.GoalsAgainstAverageHome,
                Away = row.GoalsAgainstAverageAway
            },
            CleanSheets = MapSummary(row.CleanSheetsTotal, row.CleanSheetsHome, row.CleanSheetsAway),
            FailedToScore = MapSummary(row.FailedToScoreTotal, row.FailedToScoreHome, row.FailedToScoreAway),
            Biggest = new TeamStatisticsBiggestDto
            {
                StreakWins = row.BiggestStreakWins,
                StreakDraws = row.BiggestStreakDraws,
                StreakLosses = row.BiggestStreakLosses,
                BiggestWinHome = row.BiggestWinHome,
                BiggestWinAway = row.BiggestWinAway,
                BiggestLossHome = row.BiggestLossHome,
                BiggestLossAway = row.BiggestLossAway,
                BiggestGoalsForHome = row.BiggestGoalsForHome,
                BiggestGoalsForAway = row.BiggestGoalsForAway,
                BiggestGoalsAgainstHome = row.BiggestGoalsAgainstHome,
                BiggestGoalsAgainstAway = row.BiggestGoalsAgainstAway
            },
            SyncedAtUtc = row.SyncedAtUtc
        };
    }

    private static TeamStatisticsSummaryDto MapSummary(int total, int home, int away)
    {
        return new TeamStatisticsSummaryDto
        {
            Total = total,
            Home = home,
            Away = away
        };
    }

    private static string CalculateResult(int? goalsFor, int? goalsAgainst)
    {
        if (goalsFor > goalsAgainst)
            return "W";

        if (goalsFor < goalsAgainst)
            return "L";

        return "D";
    }
}
