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

        var existingLookup = existingLeagues.ToDictionary(
            x => $"{x.ApiLeagueId}_{x.Season}",
            x => x);

        var processed = 0;

        foreach (var item in apiLeagues)
        {
            var league = item.League;
            var countryName = item.Country.Name?.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(countryName))
                continue;

            if (!countriesByName.TryGetValue(countryName, out var country))
                continue; // skip ако няма country

            foreach (var season in item.Seasons)
            {
                var key = $"{league.Id}_{season.Year}";

                if (existingLookup.TryGetValue(key, out var existing))
                {
                    // update ако трябва
                    if (existing.Name != league.Name)
                    {
                        existing.Name = league.Name;
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
                    existingLookup[key] = newLeague;
                }

                processed++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return processed;
    }
}