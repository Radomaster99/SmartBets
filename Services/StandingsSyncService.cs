using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Entities;

namespace SmartBets.Services;

public class StandingsSyncResult
{
    public int Processed { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int SkippedMissingTeams { get; set; }
}

public class StandingsSyncService
{
    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _apiService;

    public StandingsSyncService(AppDbContext dbContext, FootballApiService apiService)
    {
        _dbContext = dbContext;
        _apiService = apiService;
    }

    public async Task<StandingsSyncResult> SyncStandingsAsync(long leagueId, int season, CancellationToken cancellationToken = default)
    {
        var league = await _dbContext.Leagues
            .FirstOrDefaultAsync(x => x.ApiLeagueId == leagueId && x.Season == season, cancellationToken);

        if (league is null)
        {
            throw new InvalidOperationException($"League with apiLeagueId {leagueId} and season {season} was not found in database.");
        }

        var result = new StandingsSyncResult();

        var teams = await _dbContext.Teams.ToListAsync(cancellationToken);
        var teamsByApiId = teams.ToDictionary(x => x.ApiTeamId, x => x);

        var existingStandings = await _dbContext.Standings
            .Where(x => x.LeagueId == league.Id && x.Season == season)
            .ToListAsync(cancellationToken);

        var existingByTeamId = existingStandings.ToDictionary(x => x.TeamId, x => x);

        var apiStandings = await _apiService.GetStandingsAsync(leagueId, season, cancellationToken);

        foreach (var item in apiStandings)
        {
            if (!teamsByApiId.TryGetValue(item.Team.Id, out var team))
            {
                result.SkippedMissingTeams++;
                continue;
            }

            if (existingByTeamId.TryGetValue(team.Id, out var existing))
            {
                var isChanged = false;

                if (existing.Rank != item.Rank) { existing.Rank = item.Rank; isChanged = true; }
                if (existing.Points != item.Points) { existing.Points = item.Points; isChanged = true; }
                if (existing.GoalsDiff != item.GoalsDiff) { existing.GoalsDiff = item.GoalsDiff; isChanged = true; }
                if (existing.GroupName != item.Group) { existing.GroupName = item.Group; isChanged = true; }
                if (existing.Form != item.Form) { existing.Form = item.Form; isChanged = true; }
                if (existing.Status != item.Status) { existing.Status = item.Status; isChanged = true; }
                if (existing.Description != item.Description) { existing.Description = item.Description; isChanged = true; }

                if (existing.Played != item.All.Played) { existing.Played = item.All.Played; isChanged = true; }
                if (existing.Win != item.All.Win) { existing.Win = item.All.Win; isChanged = true; }
                if (existing.Draw != item.All.Draw) { existing.Draw = item.All.Draw; isChanged = true; }
                if (existing.Lose != item.All.Lose) { existing.Lose = item.All.Lose; isChanged = true; }
                if (existing.GoalsFor != item.All.Goals.For) { existing.GoalsFor = item.All.Goals.For; isChanged = true; }
                if (existing.GoalsAgainst != item.All.Goals.Against) { existing.GoalsAgainst = item.All.Goals.Against; isChanged = true; }

                if (isChanged)
                    result.Updated++;
            }
            else
            {
                var newStanding = new Standing
                {
                    LeagueId = league.Id,
                    Season = season,
                    TeamId = team.Id,
                    Rank = item.Rank,
                    Points = item.Points,
                    GoalsDiff = item.GoalsDiff,
                    GroupName = item.Group,
                    Form = item.Form,
                    Status = item.Status,
                    Description = item.Description,
                    Played = item.All.Played,
                    Win = item.All.Win,
                    Draw = item.All.Draw,
                    Lose = item.All.Lose,
                    GoalsFor = item.All.Goals.For,
                    GoalsAgainst = item.All.Goals.Against
                };

                _dbContext.Standings.Add(newStanding);
                existingByTeamId[team.Id] = newStanding;
                result.Inserted++;
            }

            result.Processed++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }
}