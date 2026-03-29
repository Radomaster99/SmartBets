using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Entities;

namespace SmartBets.Services;

public class TeamSyncResult
{
    public int Processed { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
}

public class TeamSyncService
{
    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _apiService;

    public TeamSyncService(AppDbContext dbContext, FootballApiService apiService)
    {
        _dbContext = dbContext;
        _apiService = apiService;
    }

    public async Task<TeamSyncResult> SyncTeamsAsync(long leagueId, int season, CancellationToken cancellationToken = default)
    {
        var leagueExists = await _dbContext.Leagues
            .AnyAsync(x => x.ApiLeagueId == leagueId && x.Season == season, cancellationToken);

        if (!leagueExists)
        {
            throw new InvalidOperationException($"League with apiLeagueId {leagueId} and season {season} was not found in database.");
        }

        var result = new TeamSyncResult();

        var countries = await _dbContext.Countries.ToListAsync(cancellationToken);
        var countriesByName = countries.ToDictionary(
            x => x.Name.Trim().ToLowerInvariant(),
            x => x);

        var existingTeams = await _dbContext.Teams.ToListAsync(cancellationToken);
        var existingByApiId = existingTeams.ToDictionary(x => x.ApiTeamId);

        var apiTeams = await _apiService.GetTeamsAsync(leagueId, season, cancellationToken);

        foreach (var item in apiTeams)
        {
            var apiTeam = item.Team;

            if (string.IsNullOrWhiteSpace(apiTeam.Name))
                continue;

            var normalizedName = apiTeam.Name.Trim();
            var normalizedCode = NormalizeNullable(apiTeam.Code);
            var normalizedLogo = NormalizeNullable(apiTeam.Logo);

            long? countryId = null;

            if (!string.IsNullOrWhiteSpace(apiTeam.Country))
            {
                var countryKey = apiTeam.Country.Trim().ToLowerInvariant();

                if (countriesByName.TryGetValue(countryKey, out var country))
                {
                    countryId = country.Id;
                }
            }

            if (existingByApiId.TryGetValue(apiTeam.Id, out var existing))
            {
                var isChanged = false;

                if (existing.Name != normalizedName)
                {
                    existing.Name = normalizedName;
                    isChanged = true;
                }

                if (existing.Code != normalizedCode)
                {
                    existing.Code = normalizedCode;
                    isChanged = true;
                }

                if (existing.LogoUrl != normalizedLogo)
                {
                    existing.LogoUrl = normalizedLogo;
                    isChanged = true;
                }

                if (existing.CountryId != countryId)
                {
                    existing.CountryId = countryId;
                    isChanged = true;
                }

                if (isChanged)
                {
                    result.Updated++;
                }
            }
            else
            {
                var newTeam = new Team
                {
                    ApiTeamId = apiTeam.Id,
                    Name = normalizedName,
                    Code = normalizedCode,
                    LogoUrl = normalizedLogo,
                    CountryId = countryId
                };

                _dbContext.Teams.Add(newTeam);
                existingByApiId[apiTeam.Id] = newTeam;
                result.Inserted++;
            }

            result.Processed++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    private static string? NormalizeNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }
}