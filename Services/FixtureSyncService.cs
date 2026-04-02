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
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ApiLeagueId == leagueId && x.Season == season, cancellationToken);

        if (league is null)
        {
            throw new InvalidOperationException($"League with apiLeagueId {leagueId} and season {season} was not found in database.");
        }

        var result = new FixtureSyncResult();
        var apiFixtures = await _apiService.GetFixturesAsync(leagueId, season, cancellationToken);
        var apiFixtureIds = apiFixtures
            .Select(x => x.Fixture.Id)
            .Distinct()
            .ToList();
        var teamApiIds = apiFixtures
            .SelectMany(x => new[] { x.Teams.Home.Id, x.Teams.Away.Id })
            .Distinct()
            .ToList();

        var teams = await _dbContext.Teams
            .AsNoTracking()
            .Where(x => teamApiIds.Contains(x.ApiTeamId))
            .Select(x => new TeamReference
            {
                Id = x.Id,
                ApiTeamId = x.ApiTeamId
            })
            .ToListAsync(cancellationToken);
        var teamsByApiId = teams.ToDictionary(x => x.ApiTeamId, x => x);

        var existingFixtures = await _dbContext.Fixtures
            .Where(x => apiFixtureIds.Contains(x.ApiFixtureId))
            .ToListAsync(cancellationToken);
        var existingByApiId = existingFixtures.ToDictionary(x => x.ApiFixtureId, x => x);

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

            if (existingByApiId.TryGetValue(apiFixture.Id, out var existing))
            {
                var isChanged = ApplyFixtureData(existing, league.Id, season, homeTeam.Id, awayTeam.Id, item, status);

                if (isChanged)
                {
                    result.Updated++;
                }
            }
            else
            {
                var newFixture = new Fixture
                {
                    ApiFixtureId = apiFixture.Id
                };

                ApplyFixtureData(newFixture, league.Id, season, homeTeam.Id, awayTeam.Id, item, status);

                _dbContext.Fixtures.Add(newFixture);
                existingByApiId[apiFixture.Id] = newFixture;
                result.Inserted++;
            }

            result.Processed++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();

        return result;
    }

    public async Task<FixtureSyncResult> SyncUpcomingFixturesAsync(long leagueId, int season, CancellationToken cancellationToken = default)
    {
        await _leagueCoverageService.EnsureFixturesSupportedAsync(leagueId, season, cancellationToken);

        var league = await _dbContext.Leagues
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ApiLeagueId == leagueId && x.Season == season, cancellationToken);

        if (league is null)
        {
            throw new InvalidOperationException($"League with apiLeagueId {leagueId} and season {season} was not found in database.");
        }

        var result = new FixtureSyncResult();
        var apiFixtures = await _apiService.GetUpcomingFixturesAsync(leagueId, season, cancellationToken);
        var apiFixtureIds = apiFixtures
            .Select(x => x.Fixture.Id)
            .Distinct()
            .ToList();
        var teamApiIds = apiFixtures
            .SelectMany(x => new[] { x.Teams.Home.Id, x.Teams.Away.Id })
            .Distinct()
            .ToList();

        var teams = await _dbContext.Teams
            .AsNoTracking()
            .Where(x => teamApiIds.Contains(x.ApiTeamId))
            .Select(x => new TeamReference
            {
                Id = x.Id,
                ApiTeamId = x.ApiTeamId
            })
            .ToListAsync(cancellationToken);
        var teamsByApiId = teams.ToDictionary(x => x.ApiTeamId, x => x);

        var existingFixtures = await _dbContext.Fixtures
            .Where(x => apiFixtureIds.Contains(x.ApiFixtureId))
            .ToListAsync(cancellationToken);
        var existingByApiId = existingFixtures.ToDictionary(x => x.ApiFixtureId, x => x);

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

            if (existingByApiId.TryGetValue(apiFixture.Id, out var existing))
            {
                var isChanged = ApplyFixtureData(existing, league.Id, season, homeTeam.Id, awayTeam.Id, item, status);

                if (isChanged)
                {
                    result.Updated++;
                }
            }
            else
            {
                var newFixture = new Fixture
                {
                    ApiFixtureId = apiFixture.Id
                };

                ApplyFixtureData(newFixture, league.Id, season, homeTeam.Id, awayTeam.Id, item, status);

                _dbContext.Fixtures.Add(newFixture);
                existingByApiId[apiFixture.Id] = newFixture;
                result.Inserted++;
            }

            result.Processed++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();

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

    internal static bool ApplyFixtureData(
        Fixture target,
        long leagueId,
        int season,
        long homeTeamId,
        long awayTeamId,
        Models.ApiFootball.ApiFootballFixtureItem source,
        string? normalizedStatus)
    {
        var isChanged = false;

        var kickoffAt = EnsureUtc(source.Fixture.Date);
        var statusLong = NormalizeNullable(source.Fixture.Status?.Long);
        var referee = NormalizeNullable(source.Fixture.Referee);
        var timezone = NormalizeNullable(source.Fixture.Timezone);
        var venueName = NormalizeNullable(source.Fixture.Venue?.Name);
        var venueCity = NormalizeNullable(source.Fixture.Venue?.City);
        var round = NormalizeNullable(source.League.Round);

        if (target.LeagueId != leagueId) { target.LeagueId = leagueId; isChanged = true; }
        if (target.Season != season) { target.Season = season; isChanged = true; }
        if (target.KickoffAt != kickoffAt) { target.KickoffAt = kickoffAt; isChanged = true; }
        if (target.Status != normalizedStatus) { target.Status = normalizedStatus; isChanged = true; }
        if (target.StatusLong != statusLong) { target.StatusLong = statusLong; isChanged = true; }
        if (target.Elapsed != source.Fixture.Status?.Elapsed) { target.Elapsed = source.Fixture.Status?.Elapsed; isChanged = true; }
        if (target.StatusExtra != source.Fixture.Status?.Extra) { target.StatusExtra = source.Fixture.Status?.Extra; isChanged = true; }
        if (target.Referee != referee) { target.Referee = referee; isChanged = true; }
        if (target.Timezone != timezone) { target.Timezone = timezone; isChanged = true; }
        if (target.VenueName != venueName) { target.VenueName = venueName; isChanged = true; }
        if (target.VenueCity != venueCity) { target.VenueCity = venueCity; isChanged = true; }
        if (target.Round != round) { target.Round = round; isChanged = true; }
        if (target.HomeTeamId != homeTeamId) { target.HomeTeamId = homeTeamId; isChanged = true; }
        if (target.AwayTeamId != awayTeamId) { target.AwayTeamId = awayTeamId; isChanged = true; }
        if (target.HomeGoals != source.Goals.Home) { target.HomeGoals = source.Goals.Home; isChanged = true; }
        if (target.AwayGoals != source.Goals.Away) { target.AwayGoals = source.Goals.Away; isChanged = true; }

        return isChanged;
    }

    private static string? NormalizeNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private sealed class TeamReference
    {
        public long Id { get; set; }
        public long ApiTeamId { get; set; }
    }
}
