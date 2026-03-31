using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Entities;

namespace SmartBets.Services;

public class FixtureLiveStatusSyncService
{
    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _apiService;
    private readonly SyncStateService _syncStateService;

    public FixtureLiveStatusSyncService(
        AppDbContext dbContext,
        FootballApiService apiService,
        SyncStateService syncStateService)
    {
        _dbContext = dbContext;
        _apiService = apiService;
        _syncStateService = syncStateService;
    }

    public async Task<LiveFixtureStatusSyncResultDto> SyncLiveFixturesAsync(
        long? leagueId = null,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var targetLeagueIds = await ResolveTargetLeagueIdsAsync(leagueId, activeOnly, cancellationToken);

        if (leagueId is null && activeOnly && targetLeagueIds.Count == 0)
        {
            return new LiveFixtureStatusSyncResultDto
            {
                ScopedToActiveSupportedLeagues = true,
                ExecutedAtUtc = nowUtc
            };
        }

        var liveFixtures = await _apiService.GetLiveFixturesAsync(
            targetLeagueIds.Count > 0 ? targetLeagueIds : null,
            cancellationToken);

        var result = new LiveFixtureStatusSyncResultDto
        {
            ScopedToActiveSupportedLeagues = leagueId is null && activeOnly,
            TargetLeagueCount = targetLeagueIds.Count,
            LiveFixturesReceived = liveFixtures.Count,
            ExecutedAtUtc = nowUtc
        };

        if (liveFixtures.Count == 0)
            return result;

        var fixtureIds = liveFixtures.Select(x => x.Fixture.Id).Distinct().ToList();
        var teamApiIds = liveFixtures
            .SelectMany(x => new[] { x.Teams.Home.Id, x.Teams.Away.Id })
            .Distinct()
            .ToList();
        var leagueSeasonKeys = liveFixtures
            .Select(x => new { x.League.Id, x.League.Season })
            .Distinct()
            .ToList();

        var leagueIds = leagueSeasonKeys.Select(x => x.Id).Distinct().ToList();
        var seasons = leagueSeasonKeys.Select(x => x.Season).Distinct().ToList();

        var leagues = await _dbContext.Leagues
            .Where(x => leagueIds.Contains(x.ApiLeagueId) && seasons.Contains(x.Season))
            .ToListAsync(cancellationToken);

        var teams = await _dbContext.Teams
            .Where(x => teamApiIds.Contains(x.ApiTeamId))
            .ToListAsync(cancellationToken);

        var existingFixtures = await _dbContext.Fixtures
            .Where(x => fixtureIds.Contains(x.ApiFixtureId))
            .ToListAsync(cancellationToken);

        var leaguesByKey = leagues.ToDictionary(x => BuildLeagueKey(x.ApiLeagueId, x.Season));
        var teamsByApiId = teams.ToDictionary(x => x.ApiTeamId);
        var fixturesByApiId = existingFixtures.ToDictionary(x => x.ApiFixtureId);
        var touchedScopes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in liveFixtures.OrderBy(x => x.Fixture.Date))
        {
            if (!leaguesByKey.TryGetValue(BuildLeagueKey(item.League.Id, item.League.Season), out var league))
            {
                result.FixturesSkippedMissingLeague++;
                continue;
            }

            if (!teamsByApiId.TryGetValue(item.Teams.Home.Id, out var homeTeam) ||
                !teamsByApiId.TryGetValue(item.Teams.Away.Id, out var awayTeam))
            {
                result.FixturesSkippedMissingTeams++;
                continue;
            }

            if (!fixturesByApiId.TryGetValue(item.Fixture.Id, out var fixture))
            {
                fixture = new Fixture
                {
                    ApiFixtureId = item.Fixture.Id
                };

                FixtureSyncService.ApplyFixtureData(
                    fixture,
                    league.Id,
                    league.Season,
                    homeTeam.Id,
                    awayTeam.Id,
                    item,
                    FixtureStatusMapper.NormalizeShort(item.Fixture.Status?.Short));

                fixture.LastLiveStatusSyncedAtUtc = nowUtc;

                _dbContext.Fixtures.Add(fixture);
                fixturesByApiId[item.Fixture.Id] = fixture;
                result.FixturesInserted++;
                result.FixturesProcessed++;
                touchedScopes.Add(BuildLeagueKey(league.ApiLeagueId, league.Season));
                continue;
            }

            var isChanged = FixtureSyncService.ApplyFixtureData(
                fixture,
                league.Id,
                league.Season,
                homeTeam.Id,
                awayTeam.Id,
                item,
                FixtureStatusMapper.NormalizeShort(item.Fixture.Status?.Short));

            if (fixture.LastLiveStatusSyncedAtUtc != nowUtc)
            {
                fixture.LastLiveStatusSyncedAtUtc = nowUtc;
            }

            if (isChanged)
            {
                result.FixturesUpdated++;
            }
            else
            {
                result.FixturesUnchanged++;
            }

            result.FixturesProcessed++;
            touchedScopes.Add(BuildLeagueKey(league.ApiLeagueId, league.Season));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var scope in touchedScopes)
        {
            var parts = scope.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                continue;

            if (long.TryParse(parts[0], out var parsedLeagueId) &&
                int.TryParse(parts[1], out var parsedSeason))
            {
                await _syncStateService.SetLastSyncedAtAsync(
                    "fixtures_live",
                    parsedLeagueId,
                    parsedSeason,
                    nowUtc,
                    cancellationToken);
            }
        }

        return result;
    }

    private async Task<List<long>> ResolveTargetLeagueIdsAsync(
        long? leagueId,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        if (leagueId.HasValue)
            return new List<long> { leagueId.Value };

        if (!activeOnly)
            return new List<long>();

        return await _dbContext.SupportedLeagues
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.LeagueApiId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    private static string BuildLeagueKey(long leagueApiId, int season)
    {
        return $"{leagueApiId}:{season}";
    }
}
