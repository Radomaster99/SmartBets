using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;

namespace SmartBets.Services;

public class DiscoveryService
{
    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _apiService;

    public DiscoveryService(AppDbContext dbContext, FootballApiService apiService)
    {
        _dbContext = dbContext;
        _apiService = apiService;
    }

    public async Task<List<LeagueCoverageDto>> CheckLeagueCoverageAsync(
        int season,
        int maxLeaguesToCheck = 10,
        CancellationToken cancellationToken = default)
    {
        var leagues = await _dbContext.Leagues
            .AsNoTracking()
            .Include(x => x.Country)
            .Where(x => x.Season == season)
            .ToListAsync(cancellationToken);

        var result = new List<LeagueCoverageDto>();

        foreach (var league in leagues)
        {
            if (result.Count >= maxLeaguesToCheck)
                break;

            try
            {
                var apiFixtures = await _apiService.GetFixturesAsync(league.ApiLeagueId, season, cancellationToken);

                if (apiFixtures.Count == 0)
                    continue;

                var fixturesCount = apiFixtures.Count;
                var upcomingCount = apiFixtures.Count(x =>
                    FixtureStatusMapper.GetStateBucket(x.Fixture.Status.Short) == Enums.FixtureStateBucket.Upcoming);
                var finishedCount = apiFixtures.Count(x =>
                    FixtureStatusMapper.GetStateBucket(x.Fixture.Status.Short) == Enums.FixtureStateBucket.Finished);
                var liveCount = apiFixtures.Count(x =>
                    FixtureStatusMapper.GetStateBucket(x.Fixture.Status.Short) == Enums.FixtureStateBucket.Live);

                result.Add(new LeagueCoverageDto
                {
                    ApiLeagueId = league.ApiLeagueId,
                    LeagueName = league.Name,
                    Season = league.Season,
                    CountryName = league.Country.Name,
                    FixturesCount = fixturesCount,
                    UpcomingCount = upcomingCount,
                    FinishedCount = finishedCount,
                    LiveCount = liveCount
                });
            }
            catch
            {
                // засега ги игнорираме
            }
        }

        return result
            .OrderByDescending(x => x.UpcomingCount)
            .ThenByDescending(x => x.FixturesCount)
            .ThenBy(x => x.CountryName)
            .ThenBy(x => x.LeagueName)
            .ToList();
    }
}
