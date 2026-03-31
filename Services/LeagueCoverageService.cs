using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Entities;

namespace SmartBets.Services;

public class LeagueCoverageService
{
    private readonly AppDbContext _dbContext;

    public LeagueCoverageService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<LeagueSeasonCoverage?> GetCoverageAsync(
        long leagueApiId,
        int season,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.LeagueSeasonCoverages
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.LeagueApiId == leagueApiId && x.Season == season,
                cancellationToken);
    }

    public async Task EnsureFixturesSupportedAsync(
        long leagueApiId,
        int season,
        CancellationToken cancellationToken = default)
    {
        var coverage = await GetCoverageAsync(leagueApiId, season, cancellationToken);

        if (coverage is not null && !coverage.HasFixtures)
        {
            throw new InvalidOperationException(
                $"Fixtures coverage is not available for league {leagueApiId} and season {season}.");
        }
    }

    public async Task EnsureStandingsSupportedAsync(
        long leagueApiId,
        int season,
        CancellationToken cancellationToken = default)
    {
        var coverage = await GetCoverageAsync(leagueApiId, season, cancellationToken);

        if (coverage is not null && !coverage.HasStandings)
        {
            throw new InvalidOperationException(
                $"Standings coverage is not available for league {leagueApiId} and season {season}.");
        }
    }

    public async Task EnsureOddsSupportedAsync(
        long leagueApiId,
        int season,
        CancellationToken cancellationToken = default)
    {
        var coverage = await GetCoverageAsync(leagueApiId, season, cancellationToken);

        if (coverage is not null && !coverage.HasOdds)
        {
            throw new InvalidOperationException(
                $"Odds coverage is not available for league {leagueApiId} and season {season}.");
        }
    }

    public async Task EnsurePredictionsSupportedAsync(
        long leagueApiId,
        int season,
        CancellationToken cancellationToken = default)
    {
        var coverage = await GetCoverageAsync(leagueApiId, season, cancellationToken);

        if (coverage is not null && !coverage.HasPredictions)
        {
            throw new InvalidOperationException(
                $"Predictions coverage is not available for league {leagueApiId} and season {season}.");
        }
    }

    public async Task EnsureInjuriesSupportedAsync(
        long leagueApiId,
        int season,
        CancellationToken cancellationToken = default)
    {
        var coverage = await GetCoverageAsync(leagueApiId, season, cancellationToken);

        if (coverage is not null && !coverage.HasInjuries)
        {
            throw new InvalidOperationException(
                $"Injuries coverage is not available for league {leagueApiId} and season {season}.");
        }
    }
}
