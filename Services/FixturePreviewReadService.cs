using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Entities;
using SmartBets.Enums;

namespace SmartBets.Services;

public class FixturePreviewReadService
{
    private readonly AppDbContext _dbContext;

    public FixturePreviewReadService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FixturePredictionDto?> GetPredictionAsync(long fixtureId, CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.FixturePredictions
            .AsNoTracking()
            .Where(x => x.FixtureId == fixtureId)
            .OrderByDescending(x => x.SyncedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return null;

        return MapPrediction(row);
    }

    public async Task<List<FixtureInjuryDto>> GetInjuriesAsync(long fixtureId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.FixtureInjuries
            .AsNoTracking()
            .Where(x => x.FixtureId == fixtureId)
            .OrderBy(x => x.TeamName)
            .ThenBy(x => x.PlayerName)
            .Select(x => new FixtureInjuryDto
            {
                TeamId = x.TeamId,
                TeamApiId = x.ApiTeamId,
                TeamName = x.TeamName,
                TeamLogoUrl = x.TeamLogoUrl,
                PlayerApiId = x.PlayerApiId,
                PlayerName = x.PlayerName,
                PlayerPhotoUrl = x.PlayerPhotoUrl,
                Type = x.Type,
                Reason = x.Reason,
                SyncedAtUtc = x.SyncedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<TeamRecentFormDto> GetRecentFormAsync(
        Fixture fixture,
        bool homeTeam,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var teamId = homeTeam ? fixture.HomeTeamId : fixture.AwayTeamId;
        var teamApiId = homeTeam ? fixture.HomeTeam.ApiTeamId : fixture.AwayTeam.ApiTeamId;
        var teamName = homeTeam ? fixture.HomeTeam.Name : fixture.AwayTeam.Name;
        var teamLogo = homeTeam ? fixture.HomeTeam.LogoUrl : fixture.AwayTeam.LogoUrl;
        var finishedStatuses = FixtureStatusMapper.GetStatusesForBucket(FixtureStateBucket.Finished).ToArray();

        var items = await _dbContext.Fixtures
            .AsNoTracking()
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            .Where(x =>
                x.Id != fixture.Id &&
                x.Season == fixture.Season &&
                x.KickoffAt < fixture.KickoffAt &&
                x.Status != null &&
                finishedStatuses.Contains(x.Status) &&
                (x.HomeTeamId == teamId || x.AwayTeamId == teamId))
            .OrderByDescending(x => x.KickoffAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var formItems = items
            .Select(x =>
            {
                var isHome = x.HomeTeamId == teamId;
                var goalsFor = isHome ? x.HomeGoals : x.AwayGoals;
                var goalsAgainst = isHome ? x.AwayGoals : x.HomeGoals;
                var result = CalculateResult(goalsFor, goalsAgainst);

                return new TeamFormItemDto
                {
                    ApiFixtureId = x.ApiFixtureId,
                    KickoffAtUtc = x.KickoffAt,
                    OpponentName = isHome ? x.AwayTeam.Name : x.HomeTeam.Name,
                    OpponentLogoUrl = isHome ? x.AwayTeam.LogoUrl : x.HomeTeam.LogoUrl,
                    IsHome = isHome,
                    GoalsFor = goalsFor,
                    GoalsAgainst = goalsAgainst,
                    Result = result
                };
            })
            .ToList();

        return new TeamRecentFormDto
        {
            TeamApiId = teamApiId,
            TeamName = teamName,
            TeamLogoUrl = teamLogo,
            Form = string.Concat(formItems.Select(x => x.Result)),
            Wins = formItems.Count(x => x.Result == "W"),
            Draws = formItems.Count(x => x.Result == "D"),
            Losses = formItems.Count(x => x.Result == "L"),
            Fixtures = formItems
        };
    }

    public async Task<FixtureHeadToHeadDto> GetHeadToHeadAsync(
        Fixture fixture,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var finishedStatuses = FixtureStatusMapper.GetStatusesForBucket(FixtureStateBucket.Finished).ToArray();

        var meetings = await _dbContext.Fixtures
            .AsNoTracking()
            .Include(x => x.League)
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            .Where(x =>
                x.Id != fixture.Id &&
                x.KickoffAt < fixture.KickoffAt &&
                x.Status != null &&
                finishedStatuses.Contains(x.Status) &&
                ((x.HomeTeamId == fixture.HomeTeamId && x.AwayTeamId == fixture.AwayTeamId) ||
                 (x.HomeTeamId == fixture.AwayTeamId && x.AwayTeamId == fixture.HomeTeamId)))
            .OrderByDescending(x => x.KickoffAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var homeWins = 0;
        var awayWins = 0;
        var draws = 0;

        foreach (var meeting in meetings)
        {
            var homeTeamGoals = meeting.HomeTeamId == fixture.HomeTeamId ? meeting.HomeGoals : meeting.AwayGoals;
            var awayTeamGoals = meeting.HomeTeamId == fixture.HomeTeamId ? meeting.AwayGoals : meeting.HomeGoals;

            if (homeTeamGoals > awayTeamGoals)
                homeWins++;
            else if (homeTeamGoals < awayTeamGoals)
                awayWins++;
            else
                draws++;
        }

        return new FixtureHeadToHeadDto
        {
            HomeTeamApiId = fixture.HomeTeam.ApiTeamId,
            HomeTeamName = fixture.HomeTeam.Name,
            AwayTeamApiId = fixture.AwayTeam.ApiTeamId,
            AwayTeamName = fixture.AwayTeam.Name,
            MeetingsCount = meetings.Count,
            HomeTeamWins = homeWins,
            AwayTeamWins = awayWins,
            Draws = draws,
            RecentMeetings = meetings
                .Select(x => new HeadToHeadItemDto
                {
                    ApiFixtureId = x.ApiFixtureId,
                    KickoffAtUtc = x.KickoffAt,
                    LeagueName = x.League.Name,
                    HomeTeamName = x.HomeTeam.Name,
                    HomeTeamLogoUrl = x.HomeTeam.LogoUrl,
                    AwayTeamName = x.AwayTeam.Name,
                    AwayTeamLogoUrl = x.AwayTeam.LogoUrl,
                    HomeGoals = x.HomeGoals,
                    AwayGoals = x.AwayGoals
                })
                .ToList()
        };
    }

    private static FixturePredictionDto MapPrediction(FixturePrediction row)
    {
        return new FixturePredictionDto
        {
            WinnerTeamApiId = row.WinnerTeamApiId,
            WinnerTeamName = row.WinnerTeamName,
            WinnerComment = row.WinnerComment,
            WinOrDraw = row.WinOrDraw,
            UnderOver = row.UnderOver,
            Advice = row.Advice,
            GoalsHome = row.GoalsHome,
            GoalsAway = row.GoalsAway,
            PercentHome = row.PercentHome,
            PercentDraw = row.PercentDraw,
            PercentAway = row.PercentAway,
            Comparison = new FixturePredictionComparisonDto
            {
                Form = new FixturePredictionComparisonPairDto
                {
                    Home = row.ComparisonFormHome,
                    Away = row.ComparisonFormAway
                },
                Attack = new FixturePredictionComparisonPairDto
                {
                    Home = row.ComparisonAttackHome,
                    Away = row.ComparisonAttackAway
                },
                Defence = new FixturePredictionComparisonPairDto
                {
                    Home = row.ComparisonDefenceHome,
                    Away = row.ComparisonDefenceAway
                },
                PoissonDistribution = new FixturePredictionComparisonPairDto
                {
                    Home = row.ComparisonPoissonHome,
                    Away = row.ComparisonPoissonAway
                },
                HeadToHead = new FixturePredictionComparisonPairDto
                {
                    Home = row.ComparisonHeadToHeadHome,
                    Away = row.ComparisonHeadToHeadAway
                },
                Goals = new FixturePredictionComparisonPairDto
                {
                    Home = row.ComparisonGoalsHome,
                    Away = row.ComparisonGoalsAway
                },
                Total = new FixturePredictionComparisonPairDto
                {
                    Home = row.ComparisonTotalHome,
                    Away = row.ComparisonTotalAway
                }
            },
            SyncedAtUtc = row.SyncedAtUtc
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
