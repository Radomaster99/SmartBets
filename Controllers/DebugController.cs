using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DebugController : ControllerBase
{
    private const string ProviderLiveFeedBookmakerName = "API-Football Live Feed";

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

        var requestedBookmakerName = bookmakerId.HasValue
            ? await _dbContext.Bookmakers
                .AsNoTracking()
                .Where(x => x.ApiBookmakerId == bookmakerId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

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
                                Id = 0L,
                                Name = !string.IsNullOrWhiteSpace(requestedBookmakerName)
                                    ? requestedBookmakerName
                                    : ProviderLiveFeedBookmakerName,
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
}
