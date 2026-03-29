using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Entities;

namespace SmartBets.Services;

public class BookmakerSyncResult
{
    public int Processed { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
}

public class BookmakerSyncService
{
    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _apiService;

    public BookmakerSyncService(AppDbContext dbContext, FootballApiService apiService)
    {
        _dbContext = dbContext;
        _apiService = apiService;
    }

    public async Task<BookmakerSyncResult> SyncBookmakersAsync(long leagueId, int season, CancellationToken cancellationToken = default)
    {
        var leagueExists = await _dbContext.Leagues
            .AnyAsync(x => x.ApiLeagueId == leagueId && x.Season == season, cancellationToken);

        if (!leagueExists)
        {
            throw new InvalidOperationException($"League with apiLeagueId {leagueId} and season {season} was not found in database.");
        }

        var oddsResponse = await _apiService.GetOddsAsync(leagueId, season, cancellationToken);

        var existingBookmakers = await _dbContext.Bookmakers.ToListAsync(cancellationToken);
        var existingByApiId = existingBookmakers.ToDictionary(x => x.ApiBookmakerId, x => x);

        var result = new BookmakerSyncResult();
        var processedBookmakerIds = new HashSet<long>();

        foreach (var fixtureItem in oddsResponse)
        {
            foreach (var apiBookmaker in fixtureItem.Bookmakers)
            {
                if (!processedBookmakerIds.Add(apiBookmaker.Id))
                    continue;

                if (existingByApiId.TryGetValue(apiBookmaker.Id, out var existing))
                {
                    if (existing.Name != apiBookmaker.Name.Trim())
                    {
                        existing.Name = apiBookmaker.Name.Trim();
                        result.Updated++;
                    }
                }
                else
                {
                    var newBookmaker = new Bookmaker
                    {
                        ApiBookmakerId = apiBookmaker.Id,
                        Name = apiBookmaker.Name.Trim()
                    };

                    _dbContext.Bookmakers.Add(newBookmaker);
                    existingByApiId[apiBookmaker.Id] = newBookmaker;
                    result.Inserted++;
                }

                result.Processed++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }
}