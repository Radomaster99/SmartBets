using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Entities;

namespace SmartBets.Services;

public class LeagueSyncService
{
    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _apiService;

    public LeagueSyncService(AppDbContext dbContext, FootballApiService apiService)
    {
        _dbContext = dbContext;
        _apiService = apiService;
    }

    public async Task<int> SyncLeaguesAsync(CancellationToken cancellationToken = default)
    {
        var apiLeagues = await _apiService.GetLeaguesAsync(cancellationToken);

        var countries = await _dbContext.Countries.ToListAsync(cancellationToken);
        var countriesByName = countries.ToDictionary(x => x.Name.ToLowerInvariant());

        var existingLeagues = await _dbContext.Leagues.ToListAsync(cancellationToken);
        var existingLeaguesLookup = existingLeagues.ToDictionary(
            x => $"{x.ApiLeagueId}_{x.Season}",
            x => x);

        var existingCoverages = await _dbContext.LeagueSeasonCoverages.ToListAsync(cancellationToken);
        var existingCoveragesLookup = existingCoverages.ToDictionary(
            x => $"{x.LeagueApiId}_{x.Season}",
            x => x);

        var processed = 0;
        var nowUtc = DateTime.UtcNow;

        foreach (var item in apiLeagues)
        {
            var league = item.League;
            var countryName = item.Country.Name?.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(countryName))
                continue;

            if (!countriesByName.TryGetValue(countryName, out var country))
                continue;

            foreach (var season in item.Seasons)
            {
                var key = $"{league.Id}_{season.Year}";

                if (existingLeaguesLookup.TryGetValue(key, out var existingLeague))
                {
                    if (existingLeague.Name != league.Name)
                    {
                        existingLeague.Name = league.Name;
                    }

                    if (existingLeague.CountryId != country.Id)
                    {
                        existingLeague.CountryId = country.Id;
                    }
                }
                else
                {
                    var newLeague = new League
                    {
                        ApiLeagueId = league.Id,
                        Name = league.Name,
                        Season = season.Year,
                        CountryId = country.Id
                    };

                    _dbContext.Leagues.Add(newLeague);
                    existingLeaguesLookup[key] = newLeague;
                }

                var apiCoverage = season.Coverage;

                if (existingCoveragesLookup.TryGetValue(key, out var existingCoverage))
                {
                    ApplyCoverage(existingCoverage, apiCoverage);
                    existingCoverage.UpdatedAt = nowUtc;
                }
                else
                {
                    var newCoverage = new LeagueSeasonCoverage
                    {
                        LeagueApiId = league.Id,
                        Season = season.Year,
                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc
                    };

                    ApplyCoverage(newCoverage, apiCoverage);

                    _dbContext.LeagueSeasonCoverages.Add(newCoverage);
                    existingCoveragesLookup[key] = newCoverage;
                }

                processed++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return processed;
    }

    private static void ApplyCoverage(
        LeagueSeasonCoverage target,
        Models.ApiFootball.ApiFootballLeagueSeasonCoverage? source)
    {
        target.HasFixtures = source is not null;
        target.HasFixtureEvents = source?.Fixtures.Events ?? false;
        target.HasLineups = source?.Fixtures.Lineups ?? false;
        target.HasFixtureStatistics = source?.Fixtures.StatisticsFixtures ?? false;
        target.HasPlayerStatistics = source?.Fixtures.StatisticsPlayers ?? false;
        target.HasStandings = source?.Standings ?? false;
        target.HasPlayers = source?.Players ?? false;
        target.HasTopScorers = source?.TopScorers ?? false;
        target.HasTopAssists = source?.TopAssists ?? false;
        target.HasTopCards = source?.TopCards ?? false;
        target.HasInjuries = source?.Injuries ?? false;
        target.HasPredictions = source?.Predictions ?? false;
        target.HasOdds = source?.Odds ?? false;
    }
}
