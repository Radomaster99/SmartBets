using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Entities;

namespace SmartBets.Services;

public class FixtureSyncResult
{
    public int Processed { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int SkippedMissingTeams { get; set; }
}

public class FixtureSyncService
{
    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _apiService;
    private readonly LeagueCoverageService _leagueCoverageService;

    public FixtureSyncService(
        AppDbContext dbContext,
        FootballApiService apiService,
        LeagueCoverageService leagueCoverageService)
    {
        _dbContext = dbContext;
        _apiService = apiService;
        _leagueCoverageService = leagueCoverageService;
    }

    public async Task<FixtureSyncResult> SyncFixturesAsync(long leagueId, int season, CancellationToken cancellationToken = default)
    {
        await _leagueCoverageService.EnsureFixturesSupportedAsync(leagueId, season, cancellationToken);

        var league = await _dbContext.Leagues
            .FirstOrDefaultAsync(x => x.ApiLeagueId == leagueId && x.Season == season, cancellationToken);

        if (league is null)
        {
            throw new InvalidOperationException($"League with apiLeagueId {leagueId} and season {season} was not found in database.");
        }

        var result = new FixtureSyncResult();

        var teams = await _dbContext.Teams.ToListAsync(cancellationToken);
        var teamsByApiId = teams.ToDictionary(x => x.ApiTeamId, x => x);

        var existingFixtures = await _dbContext.Fixtures.ToListAsync(cancellationToken);
        var existingByApiId = existingFixtures.ToDictionary(x => x.ApiFixtureId, x => x);

        var apiFixtures = await _apiService.GetFixturesAsync(leagueId, season, cancellationToken);

        foreach (var item in apiFixtures)
        {
            var apiFixture = item.Fixture;
            var homeApiTeamId = item.Teams.Home.Id;
            var awayApiTeamId = item.Teams.Away.Id;

            if (!teamsByApiId.TryGetValue(homeApiTeamId, out var homeTeam))
            {
                result.SkippedMissingTeams++;
                continue;
            }

            if (!teamsByApiId.TryGetValue(awayApiTeamId, out var awayTeam))
            {
                result.SkippedMissingTeams++;
                continue;
            }

            var status = FixtureStatusMapper.NormalizeShort(item.Fixture.Status?.Short);
            var kickoffAt = EnsureUtc(apiFixture.Date);
            var homeGoals = item.Goals.Home;
            var awayGoals = item.Goals.Away;

            if (existingByApiId.TryGetValue(apiFixture.Id, out var existing))
            {
                var isChanged = false;

                if (existing.LeagueId != league.Id)
                {
                    existing.LeagueId = league.Id;
                    isChanged = true;
                }

                if (existing.Season != season)
                {
                    existing.Season = season;
                    isChanged = true;
                }

                if (existing.KickoffAt != kickoffAt)
                {
                    existing.KickoffAt = kickoffAt;
                    isChanged = true;
                }

                if (existing.Status != status)
                {
                    existing.Status = status;
                    isChanged = true;
                }

                if (existing.HomeTeamId != homeTeam.Id)
                {
                    existing.HomeTeamId = homeTeam.Id;
                    isChanged = true;
                }

                if (existing.AwayTeamId != awayTeam.Id)
                {
                    existing.AwayTeamId = awayTeam.Id;
                    isChanged = true;
                }

                if (existing.HomeGoals != homeGoals)
                {
                    existing.HomeGoals = homeGoals;
                    isChanged = true;
                }

                if (existing.AwayGoals != awayGoals)
                {
                    existing.AwayGoals = awayGoals;
                    isChanged = true;
                }

                if (isChanged)
                {
                    result.Updated++;
                }
            }
            else
            {
                var newFixture = new Fixture
                {
                    ApiFixtureId = apiFixture.Id,
                    LeagueId = league.Id,
                    Season = season,
                    KickoffAt = kickoffAt,
                    Status = status,
                    HomeTeamId = homeTeam.Id,
                    AwayTeamId = awayTeam.Id,
                    HomeGoals = homeGoals,
                    AwayGoals = awayGoals
                };

                _dbContext.Fixtures.Add(newFixture);
                existingByApiId[apiFixture.Id] = newFixture;
                result.Inserted++;
            }

            result.Processed++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    public async Task<FixtureSyncResult> SyncUpcomingFixturesAsync(long leagueId, int season, CancellationToken cancellationToken = default)
    {
        await _leagueCoverageService.EnsureFixturesSupportedAsync(leagueId, season, cancellationToken);

        var league = await _dbContext.Leagues
            .FirstOrDefaultAsync(x => x.ApiLeagueId == leagueId && x.Season == season, cancellationToken);

        if (league is null)
        {
            throw new InvalidOperationException($"League with apiLeagueId {leagueId} and season {season} was not found in database.");
        }

        var result = new FixtureSyncResult();

        var teams = await _dbContext.Teams.ToListAsync(cancellationToken);
        var teamsByApiId = teams.ToDictionary(x => x.ApiTeamId, x => x);

        var existingFixtures = await _dbContext.Fixtures.ToListAsync(cancellationToken);
        var existingByApiId = existingFixtures.ToDictionary(x => x.ApiFixtureId, x => x);

        var apiFixtures = await _apiService.GetUpcomingFixturesAsync(leagueId, season, cancellationToken);

        foreach (var item in apiFixtures)
        {
            var apiFixture = item.Fixture;
            var homeApiTeamId = item.Teams.Home.Id;
            var awayApiTeamId = item.Teams.Away.Id;

            if (!teamsByApiId.TryGetValue(homeApiTeamId, out var homeTeam))
            {
                result.SkippedMissingTeams++;
                continue;
            }

            if (!teamsByApiId.TryGetValue(awayApiTeamId, out var awayTeam))
            {
                result.SkippedMissingTeams++;
                continue;
            }

            var status = FixtureStatusMapper.NormalizeShort(item.Fixture.Status?.Short);
            var kickoffAt = EnsureUtc(apiFixture.Date);
            var homeGoals = item.Goals.Home;
            var awayGoals = item.Goals.Away;

            if (existingByApiId.TryGetValue(apiFixture.Id, out var existing))
            {
                var isChanged = false;

                if (existing.LeagueId != league.Id)
                {
                    existing.LeagueId = league.Id;
                    isChanged = true;
                }

                if (existing.Season != season)
                {
                    existing.Season = season;
                    isChanged = true;
                }

                if (existing.KickoffAt != kickoffAt)
                {
                    existing.KickoffAt = kickoffAt;
                    isChanged = true;
                }

                if (existing.Status != status)
                {
                    existing.Status = status;
                    isChanged = true;
                }

                if (existing.HomeTeamId != homeTeam.Id)
                {
                    existing.HomeTeamId = homeTeam.Id;
                    isChanged = true;
                }

                if (existing.AwayTeamId != awayTeam.Id)
                {
                    existing.AwayTeamId = awayTeam.Id;
                    isChanged = true;
                }

                if (existing.HomeGoals != homeGoals)
                {
                    existing.HomeGoals = homeGoals;
                    isChanged = true;
                }

                if (existing.AwayGoals != awayGoals)
                {
                    existing.AwayGoals = awayGoals;
                    isChanged = true;
                }

                if (isChanged)
                {
                    result.Updated++;
                }
            }
            else
            {
                var newFixture = new Fixture
                {
                    ApiFixtureId = apiFixture.Id,
                    LeagueId = league.Id,
                    Season = season,
                    KickoffAt = kickoffAt,
                    Status = status,
                    HomeTeamId = homeTeam.Id,
                    AwayTeamId = awayTeam.Id,
                    HomeGoals = homeGoals,
                    AwayGoals = awayGoals
                };

                _dbContext.Fixtures.Add(newFixture);
                existingByApiId[apiFixture.Id] = newFixture;
                result.Inserted++;
            }

            result.Processed++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value
        };
    }
}
