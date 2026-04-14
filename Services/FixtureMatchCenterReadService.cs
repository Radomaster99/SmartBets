using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Entities;

namespace SmartBets.Services;

public class FixtureMatchCenterReadService
{
    private readonly AppDbContext _dbContext;

    public FixtureMatchCenterReadService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<FixtureEventDto>> GetEventsAsync(long fixtureId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.FixtureEvents
            .AsNoTracking()
            .Where(x => x.FixtureId == fixtureId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new FixtureEventDto
            {
                SortOrder = x.SortOrder,
                Elapsed = x.Elapsed,
                Extra = x.Extra,
                TeamId = x.TeamId,
                TeamApiId = x.ApiTeamId,
                TeamName = x.TeamName,
                TeamLogoUrl = x.TeamLogoUrl,
                PlayerApiId = x.PlayerApiId,
                PlayerName = x.PlayerName,
                AssistApiId = x.AssistApiId,
                AssistName = x.AssistName,
                Type = x.Type,
                Detail = x.Detail,
                Comments = x.Comments,
                SyncedAtUtc = x.SyncedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<FixtureTeamStatisticsDto>> GetStatisticsAsync(long fixtureId, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.FixtureStatistics
            .AsNoTracking()
            .Where(x => x.FixtureId == fixtureId)
            .OrderBy(x => x.ApiTeamId)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new { x.TeamId, x.ApiTeamId, x.TeamName, x.TeamLogoUrl, x.SyncedAtUtc })
            .Select(group => new FixtureTeamStatisticsDto
            {
                TeamId = group.Key.TeamId,
                TeamApiId = group.Key.ApiTeamId,
                TeamName = group.Key.TeamName,
                TeamLogoUrl = group.Key.TeamLogoUrl,
                SyncedAtUtc = group.Key.SyncedAtUtc,
                Statistics = group
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .Select(x => new FixtureStatisticValueDto
                    {
                        SortOrder = x.SortOrder,
                        Type = x.Type,
                        Value = x.Value
                    })
                    .ToList()
            })
            .OrderBy(x => x.TeamApiId)
            .ToList();
    }

    public async Task<FixtureCornersDto> GetCornersAsync(Fixture fixture, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.FixtureStatistics
            .AsNoTracking()
            .Where(x => x.FixtureId == fixture.Id && IsCornerStatisticType(x.Type))
            .OrderByDescending(x => x.SyncedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var homeRow = rows.FirstOrDefault(x => x.ApiTeamId == fixture.HomeTeam.ApiTeamId);
        var awayRow = rows.FirstOrDefault(x => x.ApiTeamId == fixture.AwayTeam.ApiTeamId);

        var homeCorners = ParseIntegerStatistic(homeRow?.Value);
        var awayCorners = ParseIntegerStatistic(awayRow?.Value);

        return new FixtureCornersDto
        {
            ApiFixtureId = fixture.ApiFixtureId,
            SyncedAtUtc = rows.Select(x => (DateTime?)x.SyncedAtUtc).OrderByDescending(x => x).FirstOrDefault(),
            HasData = homeRow is not null || awayRow is not null,
            TotalCorners = homeCorners.HasValue && awayCorners.HasValue
                ? homeCorners.Value + awayCorners.Value
                : null,
            Home = new FixtureCornersTeamDto
            {
                TeamId = fixture.HomeTeamId,
                TeamApiId = fixture.HomeTeam.ApiTeamId,
                TeamName = fixture.HomeTeam.Name,
                TeamLogoUrl = fixture.HomeTeam.LogoUrl,
                Corners = homeCorners
            },
            Away = new FixtureCornersTeamDto
            {
                TeamId = fixture.AwayTeamId,
                TeamApiId = fixture.AwayTeam.ApiTeamId,
                TeamName = fixture.AwayTeam.Name,
                TeamLogoUrl = fixture.AwayTeam.LogoUrl,
                Corners = awayCorners
            }
        };
    }

    public async Task<List<FixtureTeamLineupDto>> GetLineupsAsync(long fixtureId, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.FixtureLineups
            .AsNoTracking()
            .Where(x => x.FixtureId == fixtureId)
            .OrderBy(x => x.ApiTeamId)
            .ThenByDescending(x => x.IsStarting)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new
            {
                x.TeamId,
                x.ApiTeamId,
                x.TeamName,
                x.TeamLogoUrl,
                x.Formation,
                x.CoachApiId,
                x.CoachName,
                x.CoachPhotoUrl,
                x.SyncedAtUtc
            })
            .Select(group => new FixtureTeamLineupDto
            {
                TeamId = group.Key.TeamId,
                TeamApiId = group.Key.ApiTeamId,
                TeamName = group.Key.TeamName,
                TeamLogoUrl = group.Key.TeamLogoUrl,
                Formation = group.Key.Formation,
                SyncedAtUtc = group.Key.SyncedAtUtc,
                Coach = new FixtureLineupCoachDto
                {
                    ApiCoachId = group.Key.CoachApiId,
                    Name = group.Key.CoachName,
                    PhotoUrl = group.Key.CoachPhotoUrl
                },
                StartXI = group
                    .Where(x => x.IsStarting)
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .Select(MapLineupPlayer)
                    .ToList(),
                Substitutes = group
                    .Where(x => !x.IsStarting)
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .Select(MapLineupPlayer)
                    .ToList()
            })
            .OrderBy(x => x.TeamApiId)
            .ToList();
    }

    public async Task<List<FixtureTeamPlayerStatisticsDto>> GetPlayersAsync(long fixtureId, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.FixturePlayerStatistics
            .AsNoTracking()
            .Where(x => x.FixtureId == fixtureId)
            .OrderBy(x => x.ApiTeamId)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.PlayerName)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new { x.TeamId, x.ApiTeamId, x.TeamName, x.TeamLogoUrl, x.SyncedAtUtc })
            .Select(group => new FixtureTeamPlayerStatisticsDto
            {
                TeamId = group.Key.TeamId,
                TeamApiId = group.Key.ApiTeamId,
                TeamName = group.Key.TeamName,
                TeamLogoUrl = group.Key.TeamLogoUrl,
                SyncedAtUtc = group.Key.SyncedAtUtc,
                Players = group
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.PlayerName)
                    .ThenBy(x => x.Id)
                    .Select(x => new FixturePlayerMatchStatisticDto
                    {
                        SortOrder = x.SortOrder,
                        PlayerApiId = x.PlayerApiId,
                        PlayerName = x.PlayerName,
                        PlayerPhotoUrl = x.PlayerPhotoUrl,
                        Minutes = x.Minutes,
                        Number = x.Number,
                        Position = x.Position,
                        Rating = x.Rating,
                        IsCaptain = x.IsCaptain,
                        IsSubstitute = x.IsSubstitute,
                        Offsides = x.Offsides,
                        ShotsTotal = x.ShotsTotal,
                        ShotsOn = x.ShotsOn,
                        GoalsTotal = x.GoalsTotal,
                        GoalsConceded = x.GoalsConceded,
                        GoalsAssists = x.GoalsAssists,
                        GoalsSaves = x.GoalsSaves,
                        PassesTotal = x.PassesTotal,
                        PassesKey = x.PassesKey,
                        PassesAccuracy = x.PassesAccuracy,
                        TacklesTotal = x.TacklesTotal,
                        TacklesBlocks = x.TacklesBlocks,
                        TacklesInterceptions = x.TacklesInterceptions,
                        DuelsTotal = x.DuelsTotal,
                        DuelsWon = x.DuelsWon,
                        DribblesAttempts = x.DribblesAttempts,
                        DribblesSuccess = x.DribblesSuccess,
                        DribblesPast = x.DribblesPast,
                        FoulsDrawn = x.FoulsDrawn,
                        FoulsCommitted = x.FoulsCommitted,
                        CardsYellow = x.CardsYellow,
                        CardsRed = x.CardsRed,
                        PenaltyWon = x.PenaltyWon,
                        PenaltyCommitted = x.PenaltyCommitted,
                        PenaltyScored = x.PenaltyScored,
                        PenaltyMissed = x.PenaltyMissed,
                        PenaltySaved = x.PenaltySaved
                    })
                    .ToList()
            })
            .OrderBy(x => x.TeamApiId)
            .ToList();
    }

    private static FixtureLineupPlayerDto MapLineupPlayer(Entities.FixtureLineup lineup)
    {
        return new FixtureLineupPlayerDto
        {
            SortOrder = lineup.SortOrder,
            ApiPlayerId = lineup.PlayerApiId,
            Name = lineup.PlayerName,
            Number = lineup.PlayerNumber,
            Position = lineup.PlayerPosition,
            Grid = lineup.PlayerGrid
        };
    }

    private static bool IsCornerStatisticType(string? type)
    {
        return !string.IsNullOrWhiteSpace(type) &&
               type.Contains("corner", StringComparison.OrdinalIgnoreCase);
    }

    private static int? ParseIntegerStatistic(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return int.TryParse(value.Trim(), out var parsed)
            ? parsed
            : null;
    }
}
