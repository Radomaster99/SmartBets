using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Enums;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DebugController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _footballApiService;

    public DebugController(
        IConfiguration configuration,
        AppDbContext dbContext,
        FootballApiService footballApiService)
    {
        _configuration = configuration;
        _dbContext = dbContext;
        _footballApiService = footballApiService;
    }

    [HttpGet("config")]
    public IActionResult Config()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var apiBaseUrl = _configuration["ApiFootball:BaseUrl"];
        var apiKey = _configuration["ApiFootball:ApiKey"];

        return Ok(new
        {
            ConnectionStringExists = !string.IsNullOrWhiteSpace(connectionString),
            ConnectionStringPreview = string.IsNullOrWhiteSpace(connectionString)
                ? null
                : connectionString.Length > 50
                    ? connectionString.Substring(0, 50) + "..."
                    : connectionString,
            ApiBaseUrl = apiBaseUrl,
            ApiKeyExists = !string.IsNullOrWhiteSpace(apiKey)
        });
    }

    [HttpGet("db")]
    public async Task<IActionResult> Db(CancellationToken cancellationToken)
    {
        return Ok(new
        {
            CanConnect = await _dbContext.Database.CanConnectAsync(cancellationToken),
            Countries = await _dbContext.Countries.CountAsync(cancellationToken),
            Leagues = await _dbContext.Leagues.CountAsync(cancellationToken),
            Teams = await _dbContext.Teams.CountAsync(cancellationToken),
            Fixtures = await _dbContext.Fixtures.CountAsync(cancellationToken),
            SupportedLeagues = await _dbContext.SupportedLeagues.CountAsync(cancellationToken),
            LiveOdds = await _dbContext.LiveOdds.CountAsync(cancellationToken)
        });
    }

    [HttpGet("provider/live-odds")]
    public async Task<IActionResult> ProviderLiveOdds(
        [FromQuery] long? fixtureId,
        [FromQuery] long? leagueId,
        [FromQuery] long? betId,
        [FromQuery] long? bookmakerId,
        CancellationToken cancellationToken)
    {
        if (!fixtureId.HasValue && !leagueId.HasValue)
            return BadRequest("Provide fixtureId or leagueId.");

        var response = await _footballApiService.GetLiveOddsAsync(
            fixtureId,
            leagueId,
            betId,
            bookmakerId,
            cancellationToken);

        var directOddsBookmaker = await ResolveSingleSourceLiveBookmakerAsync(cancellationToken);

        var fixtureIds = response
            .Select(x => x.Fixture.Id)
            .Distinct()
            .ToList();

        var localFixtures = await _dbContext.Fixtures
            .AsNoTracking()
            .Where(x => fixtureIds.Contains(x.ApiFixtureId))
            .Select(x => new
            {
                x.ApiFixtureId,
                LeagueApiId = x.League.ApiLeagueId,
                x.Season,
                x.Status,
                x.KickoffAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            FixtureId = fixtureId,
            LeagueId = leagueId,
            BetId = betId,
            BookmakerId = bookmakerId,
            DirectOddsResolvedBookmaker = new
            {
                directOddsBookmaker.ApiBookmakerId,
                directOddsBookmaker.Name
            },
            ProviderFixturesReceived = response.Count,
            ProviderFixtureApiIds = fixtureIds.Take(20).ToList(),
            ProviderBookmakersReceived = response.Sum(x => x.Bookmakers.Count > 0 ? x.Bookmakers.Count : x.Odds.Count > 0 ? 1 : 0),
            ProviderBetsReceived = response.Sum(x => x.Bookmakers.Count > 0 ? x.Bookmakers.Sum(y => y.Bets.Count) : x.Odds.Count),
            ProviderValuesReceived = response.Sum(x => x.Bookmakers.Count > 0 ? x.Bookmakers.Sum(y => y.Bets.Sum(z => z.Values.Count)) : x.Odds.Sum(y => y.Values.Count)),
            Sample = response
                .Take(5)
                .Select(x => new
                {
                    FixtureId = x.Fixture.Id,
                    LeagueId = x.League.Id,
                    Season = x.League.Season,
                    Bookmakers = (x.Bookmakers.Count > 0
                        ? x.Bookmakers.Take(3).Select(y => new
                        {
                            y.Id,
                            y.Name,
                            Bets = y.Bets.Take(3).Select(z => new
                            {
                                z.Id,
                                z.Name,
                                Values = z.Values.Take(5).Select(v => new
                                {
                                    v.Value,
                                    v.Odd,
                                    v.Main,
                                    v.Handicap,
                                    v.Suspended
                                })
                            })
                        })
                        : new[]
                        {
                            new
                            {
                                Id = directOddsBookmaker.ApiBookmakerId,
                                Name = directOddsBookmaker.Name,
                                Bets = x.Odds.Take(3).Select(z => new
                                {
                                    z.Id,
                                    z.Name,
                                    Values = z.Values.Take(5).Select(v => new
                                    {
                                        v.Value,
                                        v.Odd,
                                        v.Main,
                                        v.Handicap,
                                        v.Suspended
                                    })
                                })
                            }
                        })
                }),
            LocalMatchingFixtures = localFixtures
        });
    }

    private async Task<ResolvedBookmakerIdentity> ResolveSingleSourceLiveBookmakerAsync(CancellationToken cancellationToken)
    {
        var bookmakers = await _dbContext.Bookmakers
            .AsNoTracking()
            .Select(x => new { x.ApiBookmakerId, x.Name })
            .ToListAsync(cancellationToken);

        var bookmaker = bookmakers
            .Where(x => SingleSourceLiveBookmakerIdentity.IsSingleSourceName(x.Name))
            .OrderByDescending(x => x.ApiBookmakerId > 0)
            .ThenBy(x => x.ApiBookmakerId)
            .FirstOrDefault();

        if (bookmaker is not null)
        {
            return new ResolvedBookmakerIdentity(bookmaker.ApiBookmakerId, bookmaker.Name);
        }

        return new ResolvedBookmakerIdentity(
            SingleSourceLiveBookmakerIdentity.SyntheticApiBookmakerId,
            SingleSourceLiveBookmakerIdentity.Name);
    }

    [HttpGet("provider/live-odds/candidates")]
    public async Task<IActionResult> ProviderLiveOddsCandidates(
        [FromQuery] int maxLeagues = 4,
        [FromQuery] int maxFixtures = 10,
        CancellationToken cancellationToken = default)
    {
        maxLeagues = Math.Clamp(maxLeagues, 1, 10);
        maxFixtures = Math.Clamp(maxFixtures, 1, 25);

        var liveStatuses = FixtureStatusMapper
            .GetStatusesForBucket(FixtureStateBucket.Live)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var localLiveFixtures = await _dbContext.Fixtures
            .AsNoTracking()
            .Where(x => x.Status != null && liveStatuses.Contains(x.Status))
            .Select(x => new
            {
                x.Id,
                x.ApiFixtureId,
                LeagueApiId = x.League.ApiLeagueId,
                x.Season,
                x.Status,
                x.KickoffAt,
                HomeTeamName = x.HomeTeam.Name,
                AwayTeamName = x.AwayTeam.Name
            })
            .ToListAsync(cancellationToken);

        if (localLiveFixtures.Count == 0)
        {
            return Ok(new
            {
                LocalLiveFixtures = 0,
                ProviderFixturesReceived = 0,
                Candidates = Array.Empty<object>()
            });
        }

        var targetLeagueApiIds = localLiveFixtures
            .Select(x => x.LeagueApiId)
            .Distinct()
            .OrderBy(x => x)
            .Take(maxLeagues)
            .ToList();

        var resolvedLiveBookmaker = await ResolveSingleSourceLiveBookmakerAsync(cancellationToken);

        var scopedLocalFixtures = localLiveFixtures
            .Where(x => targetLeagueApiIds.Contains(x.LeagueApiId))
            .ToList();

        var providerStatsByFixtureApiId = new Dictionary<long, ProviderFixtureStats>();
        var providerFixturesReceived = 0;

        foreach (var leagueApiId in targetLeagueApiIds)
        {
            var providerRows = await _footballApiService.GetLiveOddsAsync(
                fixtureId: null,
                leagueId: leagueApiId,
                betId: null,
                bookmakerId: null,
                cancellationToken: cancellationToken);

            providerFixturesReceived += providerRows.Count;

            foreach (var row in providerRows)
            {
                providerStatsByFixtureApiId[row.Fixture.Id] = new ProviderFixtureStats
                {
                    ProviderBookmakersReceived = row.Bookmakers.Count > 0 ? row.Bookmakers.Count : row.Odds.Count > 0 ? 1 : 0,
                    ProviderBetsReceived = row.Bookmakers.Count > 0 ? row.Bookmakers.Sum(x => x.Bets.Count) : row.Odds.Count,
                    ProviderValuesReceived = row.Bookmakers.Count > 0
                        ? row.Bookmakers.Sum(x => x.Bets.Sum(y => y.Values.Count))
                        : row.Odds.Sum(x => x.Values.Count)
                };
            }
        }

        var localFixtureIds = scopedLocalFixtures.Select(x => x.Id).ToList();

        var storedLiveBookmakers = await _dbContext.LiveOdds
            .AsNoTracking()
            .Where(x => localFixtureIds.Contains(x.FixtureId))
            .GroupBy(x => new { x.FixtureId, x.Bookmaker.ApiBookmakerId })
            .Select(x => new
            {
                x.Key.FixtureId,
                x.Key.ApiBookmakerId,
                LastCollectedAtUtc = x.Max(y => y.CollectedAtUtc)
            })
            .ToListAsync(cancellationToken);

        var candidates = scopedLocalFixtures
            .Select(x =>
            {
                providerStatsByFixtureApiId.TryGetValue(x.ApiFixtureId, out var providerStats);

                var storedLiveBookmakerApiIds = storedLiveBookmakers
                    .Where(y => y.FixtureId == x.Id && y.ApiBookmakerId > 0)
                    .OrderByDescending(y => y.LastCollectedAtUtc)
                    .ThenBy(y => y.ApiBookmakerId)
                    .Select(y => y.ApiBookmakerId)
                    .Take(5)
                    .ToList();

                var hasSyntheticStoredRows = storedLiveBookmakers.Any(y => y.FixtureId == x.Id && y.ApiBookmakerId == 0);

                return new
                {
                    x.ApiFixtureId,
                    x.LeagueApiId,
                    x.Season,
                    x.Status,
                    KickoffAtUtc = x.KickoffAt,
                    x.HomeTeamName,
                    x.AwayTeamName,
                    ProviderHasFixture = providerStats is not null,
                    ProviderBookmakersReceived = providerStats?.ProviderBookmakersReceived ?? 0,
                    ProviderBetsReceived = providerStats?.ProviderBetsReceived ?? 0,
                    ProviderValuesReceived = providerStats?.ProviderValuesReceived ?? 0,
                    StoredLiveBookmakerApiIds = storedLiveBookmakerApiIds,
                    StoredLiveHasRealBookmakers = storedLiveBookmakerApiIds.Count > 0,
                    StoredLiveHasSyntheticRows = hasSyntheticStoredRows
                };
            })
            .OrderByDescending(x => x.ProviderValuesReceived > 0)
            .ThenByDescending(x => x.ProviderHasFixture)
            .ThenByDescending(x => x.KickoffAtUtc)
            .Take(maxFixtures)
            .ToList();

        return Ok(new
        {
            LocalLiveFixtures = localLiveFixtures.Count,
            ScopedLeaguesChecked = targetLeagueApiIds,
            ProviderFixturesReceived = providerFixturesReceived,
            ResolvedLiveBookmaker = new
            {
                resolvedLiveBookmaker.ApiBookmakerId,
                resolvedLiveBookmaker.Name
            },
            Candidates = candidates
        });
    }

    private sealed class ProviderFixtureStats
    {
        public int ProviderBookmakersReceived { get; set; }
        public int ProviderBetsReceived { get; set; }
        public int ProviderValuesReceived { get; set; }
    }

    private sealed record ResolvedBookmakerIdentity(long ApiBookmakerId, string Name);
}
