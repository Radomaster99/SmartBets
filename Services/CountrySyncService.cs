using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Entities;

namespace SmartBets.Services;

public class CountrySyncService
{
    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _footballApiService;

    public CountrySyncService(AppDbContext dbContext, FootballApiService footballApiService)
    {
        _dbContext = dbContext;
        _footballApiService = footballApiService;
    }

    public async Task<CountrySyncResult> SyncCountriesAsync(CancellationToken cancellationToken = default)
    {
        var apiCountries = await _footballApiService.GetCountriesAsync(cancellationToken);

        Console.WriteLine($"[DEBUG] API countries count: {apiCountries?.Count ?? -1}");

        if (apiCountries == null || apiCountries.Count == 0)
        {
            Console.WriteLine("[WARNING] API returned 0 countries!");
        }

        var existingCountries = await _dbContext.Countries.ToListAsync(cancellationToken);

        Console.WriteLine($"[DEBUG] Existing DB countries count: {existingCountries.Count}");

        var existingByName = existingCountries.ToDictionary(
            x => NormalizeName(x.Name),
            x => x);

        var result = new CountrySyncResult();

        foreach (var apiCountry in apiCountries)
        {
            if (string.IsNullOrWhiteSpace(apiCountry.Name))
                continue;

            var normalizedName = NormalizeName(apiCountry.Name);

            if (existingByName.TryGetValue(normalizedName, out var existingCountry))
            {
                var isChanged = false;

                var newCode = NormalizeNullable(apiCountry.Code);
                var newFlag = NormalizeNullable(apiCountry.Flag);

                if (existingCountry.Code != newCode)
                {
                    existingCountry.Code = newCode;
                    isChanged = true;
                }

                if (existingCountry.FlagUrl != newFlag)
                {
                    existingCountry.FlagUrl = newFlag;
                    isChanged = true;
                }

                if (isChanged)
                {
                    result.Updated++;
                }
            }
            else
            {
                var newCountry = new Country
                {
                    Name = apiCountry.Name.Trim(),
                    Code = NormalizeNullable(apiCountry.Code),
                    FlagUrl = NormalizeNullable(apiCountry.Flag)
                };

                _dbContext.Countries.Add(newCountry);
                existingByName[normalizedName] = newCountry;
                result.Inserted++;
            }

            result.Processed++;
        }

        Console.WriteLine($"[DEBUG] Processed: {result.Processed}, Inserted: {result.Inserted}, Updated: {result.Updated}");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    private static string NormalizeName(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }
}