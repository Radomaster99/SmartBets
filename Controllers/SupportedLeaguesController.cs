using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Entities;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/supported-leagues")]
public class SupportedLeaguesController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public SupportedLeaguesController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? season,
        [FromQuery] long? leagueId,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var supportedLeaguesQuery = _dbContext.SupportedLeagues
            .AsNoTracking()
            .AsQueryable();

        if (activeOnly)
        {
            supportedLeaguesQuery = supportedLeaguesQuery.Where(x => x.IsActive);
        }

        if (season.HasValue)
        {
            supportedLeaguesQuery = supportedLeaguesQuery.Where(x => x.Season == season.Value);
        }

        if (leagueId.HasValue)
        {
            supportedLeaguesQuery = supportedLeaguesQuery.Where(x => x.LeagueApiId == leagueId.Value);
        }

        var supportedLeagues = await supportedLeaguesQuery
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.LeagueApiId)
            .ToListAsync(cancellationToken);

        var syncStates = await _dbContext.SyncStates
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var leagues = await _dbContext.Leagues
            .AsNoTracking()
            .Include(x => x.Country)
            .Where(x => supportedLeagues.Select(y => y.LeagueApiId).Contains(x.ApiLeagueId) &&
                        supportedLeagues.Select(y => y.Season).Contains(x.Season))
            .ToListAsync(cancellationToken);

        var coverages = await _dbContext.LeagueSeasonCoverages
            .AsNoTracking()
            .Where(x => supportedLeagues.Select(y => y.LeagueApiId).Contains(x.LeagueApiId) &&
                        supportedLeagues.Select(y => y.Season).Contains(x.Season))
            .ToListAsync(cancellationToken);

        var leagueLookup = leagues.ToDictionary(x => $"{x.ApiLeagueId}:{x.Season}", x => x);
        var coverageLookup = coverages.ToDictionary(x => $"{x.LeagueApiId}:{x.Season}", x => x);
        var syncLookup = syncStates.ToDictionary(
            x => $"{x.EntityType}:{x.LeagueApiId?.ToString() ?? "global"}:{x.Season?.ToString() ?? "global"}",
            x => x.LastSyncedAt);

        DateTime? GetLastSyncedAt(string entityType, long leagueApiId, int supportedSeason)
        {
            var key = $"{entityType}:{leagueApiId}:{supportedSeason}";
            return syncLookup.TryGetValue(key, out var lastSyncedAt) ? lastSyncedAt : null;
        }

        var result = supportedLeagues
            .Select(x =>
            {
                leagueLookup.TryGetValue($"{x.LeagueApiId}:{x.Season}", out var league);
                coverageLookup.TryGetValue($"{x.LeagueApiId}:{x.Season}", out var coverage);

                return new SupportedLeagueDto
                {
                    Id = x.Id,
                    LeagueApiId = x.LeagueApiId,
                    Season = x.Season,
                    IsActive = x.IsActive,
                    Priority = x.Priority,
                    CreatedAtUtc = x.CreatedAt,
                    LeagueName = league?.Name ?? string.Empty,
                    CountryName = league?.Country.Name ?? string.Empty,
                    Coverage = coverage is null ? null : MapCoverage(coverage),
                    Sync = new SupportedLeagueSyncSummaryDto
                    {
                        TeamsLastSyncedAtUtc = GetLastSyncedAt("teams", x.LeagueApiId, x.Season),
                        FixturesUpcomingLastSyncedAtUtc = GetLastSyncedAt("fixtures_upcoming", x.LeagueApiId, x.Season),
                        FixturesFullLastSyncedAtUtc = GetLastSyncedAt("fixtures_full", x.LeagueApiId, x.Season),
                        EventsLastSyncedAtUtc = GetLastSyncedAt("fixture_events", x.LeagueApiId, x.Season),
                        StatisticsLastSyncedAtUtc = GetLastSyncedAt("fixture_statistics", x.LeagueApiId, x.Season),
                        LineupsLastSyncedAtUtc = GetLastSyncedAt("fixture_lineups", x.LeagueApiId, x.Season),
                        PlayerStatisticsLastSyncedAtUtc = GetLastSyncedAt("fixture_player_statistics", x.LeagueApiId, x.Season),
                        PredictionsLastSyncedAtUtc = GetLastSyncedAt("fixture_predictions", x.LeagueApiId, x.Season),
                        InjuriesLastSyncedAtUtc = GetLastSyncedAt("fixture_injuries", x.LeagueApiId, x.Season),
                        TeamStatisticsLastSyncedAtUtc = GetLastSyncedAt("team_statistics", x.LeagueApiId, x.Season),
                        RoundsLastSyncedAtUtc = GetLastSyncedAt("league_rounds", x.LeagueApiId, x.Season),
                        TopScorersLastSyncedAtUtc = GetLastSyncedAt("league_top_scorers", x.LeagueApiId, x.Season),
                        TopAssistsLastSyncedAtUtc = GetLastSyncedAt("league_top_assists", x.LeagueApiId, x.Season),
                        TopCardsLastSyncedAtUtc = GetLastSyncedAt("league_top_cards", x.LeagueApiId, x.Season),
                        StandingsLastSyncedAtUtc = GetLastSyncedAt("standings", x.LeagueApiId, x.Season),
                        OddsLastSyncedAtUtc = GetLastSyncedAt("odds", x.LeagueApiId, x.Season),
                        OddsAnalyticsLastSyncedAtUtc = GetLastSyncedAt("odds_analytics", x.LeagueApiId, x.Season),
                        BookmakersLastSyncedAtUtc = GetLastSyncedAt("bookmakers", x.LeagueApiId, x.Season)
                    }
                };
            })
            .ToList();

        return Ok(result);
    }

    private static LeagueCoverageFlagsDto MapCoverage(LeagueSeasonCoverage coverage)
    {
        return new LeagueCoverageFlagsDto
        {
            HasFixtures = coverage.HasFixtures,
            HasFixtureEvents = coverage.HasFixtureEvents,
            HasLineups = coverage.HasLineups,
            HasFixtureStatistics = coverage.HasFixtureStatistics,
            HasPlayerStatistics = coverage.HasPlayerStatistics,
            HasStandings = coverage.HasStandings,
            HasPlayers = coverage.HasPlayers,
            HasTopScorers = coverage.HasTopScorers,
            HasTopAssists = coverage.HasTopAssists,
            HasTopCards = coverage.HasTopCards,
            HasInjuries = coverage.HasInjuries,
            HasPredictions = coverage.HasPredictions,
            HasOdds = coverage.HasOdds
        };
    }
}
