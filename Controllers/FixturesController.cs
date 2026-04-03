using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Entities;
using SmartBets.Enums;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FixturesController : ControllerBase
{
    private readonly FixtureSyncService _fixtureSyncService;
    private readonly FixtureLiveStatusSyncService _fixtureLiveStatusSyncService;
    private readonly FixtureMatchCenterReadService _fixtureMatchCenterReadService;
    private readonly FixtureMatchCenterSyncService _fixtureMatchCenterSyncService;
    private readonly FixturePreviewReadService _fixturePreviewReadService;
    private readonly FixturePreviewSyncService _fixturePreviewSyncService;
    private readonly PreMatchOddsService _preMatchOddsService;
    private readonly LiveOddsService _liveOddsService;
    private readonly OddsAnalyticsService _oddsAnalyticsService;
    private readonly AppDbContext _dbContext;
    private readonly SyncStateService _syncStateService;

    public FixturesController(
        FixtureSyncService fixtureSyncService,
        FixtureLiveStatusSyncService fixtureLiveStatusSyncService,
        FixtureMatchCenterReadService fixtureMatchCenterReadService,
        FixtureMatchCenterSyncService fixtureMatchCenterSyncService,
        FixturePreviewReadService fixturePreviewReadService,
        FixturePreviewSyncService fixturePreviewSyncService,
        PreMatchOddsService preMatchOddsService,
        LiveOddsService liveOddsService,
        OddsAnalyticsService oddsAnalyticsService,
        AppDbContext dbContext,
        SyncStateService syncStateService)
    {
        _fixtureSyncService = fixtureSyncService ?? throw new ArgumentNullException(nameof(fixtureSyncService));
        _fixtureLiveStatusSyncService = fixtureLiveStatusSyncService ?? throw new ArgumentNullException(nameof(fixtureLiveStatusSyncService));
        _fixtureMatchCenterReadService = fixtureMatchCenterReadService ?? throw new ArgumentNullException(nameof(fixtureMatchCenterReadService));
        _fixtureMatchCenterSyncService = fixtureMatchCenterSyncService ?? throw new ArgumentNullException(nameof(fixtureMatchCenterSyncService));
        _fixturePreviewReadService = fixturePreviewReadService ?? throw new ArgumentNullException(nameof(fixturePreviewReadService));
        _fixturePreviewSyncService = fixturePreviewSyncService ?? throw new ArgumentNullException(nameof(fixturePreviewSyncService));
        _preMatchOddsService = preMatchOddsService ?? throw new ArgumentNullException(nameof(preMatchOddsService));
        _liveOddsService = liveOddsService ?? throw new ArgumentNullException(nameof(liveOddsService));
        _oddsAnalyticsService = oddsAnalyticsService ?? throw new ArgumentNullException(nameof(oddsAnalyticsService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _syncStateService = syncStateService ?? throw new ArgumentNullException(nameof(syncStateService));
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(
        [FromQuery] int season,
        [FromQuery] long? leagueId,
        [FromQuery] int maxLeagues = 5,
        CancellationToken cancellationToken = default)
    {
        if (season <= 0)
            return BadRequest("season must be greater than 0.");

        if (leagueId.HasValue && leagueId.Value <= 0)
            return BadRequest("leagueId must be greater than 0.");

        if (maxLeagues <= 0)
            return BadRequest("maxLeagues must be greater than 0.");

        var syncedAtUtc = DateTime.UtcNow;

        if (leagueId.HasValue)
        {
            var result = await _fixtureSyncService.SyncFixturesAsync(
                leagueId.Value,
                season,
                cancellationToken);

            await _syncStateService.SetLastSyncedAtAsync(
                "fixtures_full",
                leagueId.Value,
                season,
                syncedAtUtc,
                cancellationToken);

            return Ok(new
            {
                Message = "Fixtures synced for specific league.",
                LeagueId = leagueId,
                Season = season,
                LastSyncedAtUtc = syncedAtUtc,
                result.Processed,
                result.Inserted,
                result.Updated,
                result.SkippedMissingTeams
            });
        }

        var leagues = await _dbContext.Leagues
            .AsNoTracking()
            .Where(x => x.Season == season)
            .OrderBy(x => x.ApiLeagueId)
            .Take(maxLeagues)
            .ToListAsync(cancellationToken);

        var totalProcessed = 0;
        var totalInserted = 0;
        var totalUpdated = 0;
        var totalSkippedMissingTeams = 0;

        foreach (var league in leagues)
        {
            var result = await _fixtureSyncService.SyncFixturesAsync(
                league.ApiLeagueId,
                season,
                cancellationToken);

            await _syncStateService.SetLastSyncedAtAsync(
                "fixtures_full",
                league.ApiLeagueId,
                season,
                syncedAtUtc,
                cancellationToken);

            totalProcessed += result.Processed;
            totalInserted += result.Inserted;
            totalUpdated += result.Updated;
            totalSkippedMissingTeams += result.SkippedMissingTeams;
        }

        return Ok(new
        {
            Message = "Fixtures synced for multiple leagues.",
            Season = season,
            LeaguesProcessed = leagues.Count,
            LastSyncedAtUtc = syncedAtUtc,
            TotalProcessed = totalProcessed,
            TotalInserted = totalInserted,
            TotalUpdated = totalUpdated,
            TotalSkippedMissingTeams = totalSkippedMissingTeams
        });
    }

    [HttpPost("sync-upcoming")]
    public async Task<IActionResult> SyncUpcoming(
        [FromQuery] long leagueId,
        [FromQuery] int season,
        CancellationToken cancellationToken)
    {
        if (leagueId <= 0)
            return BadRequest("leagueId must be greater than 0.");

        if (season <= 0)
            return BadRequest("season must be greater than 0.");

        var result = await _fixtureSyncService.SyncUpcomingFixturesAsync(
            leagueId,
            season,
            cancellationToken);

        var syncedAtUtc = DateTime.UtcNow;

        await _syncStateService.SetLastSyncedAtAsync(
            "fixtures_upcoming",
            leagueId,
            season,
            syncedAtUtc,
            cancellationToken);

        return Ok(new
        {
            Message = "Upcoming fixtures synced successfully.",
            LeagueId = leagueId,
            Season = season,
            LastSyncedAtUtc = syncedAtUtc,
            result.Processed,
            result.Inserted,
            result.Updated,
            result.SkippedMissingTeams
        });
    }

    [HttpPost("sync-live-status")]
    public async Task<IActionResult> SyncLiveStatus(
        [FromQuery] long? leagueId,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        if (leagueId.HasValue && leagueId.Value <= 0)
            return BadRequest("leagueId must be greater than 0.");

        var result = await _fixtureLiveStatusSyncService.SyncLiveFixturesAsync(
            leagueId,
            activeOnly,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{apiFixtureId:long}/sync-match-center")]
    public async Task<IActionResult> SyncMatchCenter(
        long apiFixtureId,
        [FromQuery] bool includePlayers = true,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (apiFixtureId <= 0)
            return BadRequest("apiFixtureId must be greater than 0.");

        var result = await _fixtureMatchCenterSyncService.SyncFixtureAsync(
            apiFixtureId,
            includePlayers,
            force,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("sync-live-match-center")]
    public async Task<IActionResult> SyncLiveMatchCenter(
        [FromQuery] long? leagueId,
        [FromQuery] int? season,
        [FromQuery] int maxFixtures = 10,
        [FromQuery] bool includePlayers = false,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (leagueId.HasValue && leagueId.Value <= 0)
            return BadRequest("leagueId must be greater than 0.");

        if (season.HasValue && season.Value <= 0)
            return BadRequest("season must be greater than 0.");

        if (maxFixtures <= 0)
            return BadRequest("maxFixtures must be greater than 0.");

        var result = await _fixtureMatchCenterSyncService.SyncLiveFixturesAsync(
            leagueId,
            season,
            maxFixtures,
            includePlayers,
            force,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{apiFixtureId:long}/sync-preview")]
    public async Task<IActionResult> SyncPreview(
        long apiFixtureId,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (apiFixtureId <= 0)
            return BadRequest("apiFixtureId must be greater than 0.");

        var result = await _fixturePreviewSyncService.SyncFixtureAsync(
            apiFixtureId,
            force,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("sync-upcoming-previews")]
    public async Task<IActionResult> SyncUpcomingPreviews(
        [FromQuery] long? leagueId,
        [FromQuery] int? season,
        [FromQuery] int windowHours = 24,
        [FromQuery] int maxFixtures = 10,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (leagueId.HasValue && leagueId.Value <= 0)
            return BadRequest("leagueId must be greater than 0.");

        if (season.HasValue && season.Value <= 0)
            return BadRequest("season must be greater than 0.");

        if (windowHours <= 0)
            return BadRequest("windowHours must be greater than 0.");

        if (maxFixtures <= 0)
            return BadRequest("maxFixtures must be greater than 0.");

        var result = await _fixturePreviewSyncService.SyncUpcomingFixturesAsync(
            leagueId,
            season,
            windowHours,
            maxFixtures,
            force,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] long? leagueId,
        [FromQuery] int? season,
        [FromQuery] string? status,
        [FromQuery] FixtureStateBucket? state,
        [FromQuery] long? teamId,
        [FromQuery] DateOnly? date,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string direction = "asc",
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateDateFilters(date, from, to);
        if (validationError is not null)
            return BadRequest(validationError);

        var query = BuildFilteredQuery(leagueId, season, status, state, teamId, date, from, to);
        query = ApplySort(query, direction);

        var fixtures = await query.ToListAsync(cancellationToken);

        return Ok(fixtures.Select(MapFixture).ToList());
    }

    [HttpGet("query")]
    public async Task<IActionResult> Query(
        [FromQuery] long? leagueId,
        [FromQuery] int? season,
        [FromQuery] string? status,
        [FromQuery] FixtureStateBucket? state,
        [FromQuery] long? teamId,
        [FromQuery] DateOnly? date,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string direction = "asc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateDateFilters(date, from, to);
        if (validationError is not null)
            return BadRequest(validationError);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = BuildFilteredQuery(leagueId, season, status, state, teamId, date, from, to);
        var totalItems = await query.CountAsync(cancellationToken);

        query = ApplySort(query, direction);

        var fixtures = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)pageSize);

        return Ok(new PagedResultDto<FixtureDto>
        {
            Items = fixtures.Select(MapFixture).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            HasNextPage = totalPages > 0 && page < totalPages,
            HasPreviousPage = totalPages > 0 && page > 1
        });
    }

    [HttpGet("{apiFixtureId:long}")]
    public async Task<IActionResult> GetByApiFixtureId(
        long apiFixtureId,
        [FromQuery] string? marketName,
        CancellationToken cancellationToken = default)
    {
        var fixture = await GetFixtureWithRelationsAsync(apiFixtureId, cancellationToken);

        if (fixture is null)
        {
            return NotFound(new
            {
                Message = "Fixture not found."
            });
        }

        return Ok(await BuildFixtureDetailAsync(fixture, marketName, cancellationToken));
    }

    [HttpGet("{apiFixtureId:long}/events")]
    public async Task<IActionResult> GetFixtureEvents(
        long apiFixtureId,
        CancellationToken cancellationToken = default)
    {
        var fixture = await GetFixtureWithRelationsAsync(apiFixtureId, cancellationToken);
        if (fixture is null)
            return NotFound(new { Message = "Fixture not found." });

        var events = await _fixtureMatchCenterReadService.GetEventsAsync(fixture.Id, cancellationToken);
        return Ok(events);
    }

    [HttpGet("{apiFixtureId:long}/statistics")]
    public async Task<IActionResult> GetFixtureStatistics(
        long apiFixtureId,
        CancellationToken cancellationToken = default)
    {
        var fixture = await GetFixtureWithRelationsAsync(apiFixtureId, cancellationToken);
        if (fixture is null)
            return NotFound(new { Message = "Fixture not found." });

        var statistics = await _fixtureMatchCenterReadService.GetStatisticsAsync(fixture.Id, cancellationToken);
        return Ok(statistics);
    }

    [HttpGet("{apiFixtureId:long}/lineups")]
    public async Task<IActionResult> GetFixtureLineups(
        long apiFixtureId,
        CancellationToken cancellationToken = default)
    {
        var fixture = await GetFixtureWithRelationsAsync(apiFixtureId, cancellationToken);
        if (fixture is null)
            return NotFound(new { Message = "Fixture not found." });

        var lineups = await _fixtureMatchCenterReadService.GetLineupsAsync(fixture.Id, cancellationToken);
        return Ok(lineups);
    }

    [HttpGet("{apiFixtureId:long}/players")]
    public async Task<IActionResult> GetFixturePlayers(
        long apiFixtureId,
        CancellationToken cancellationToken = default)
    {
        var fixture = await GetFixtureWithRelationsAsync(apiFixtureId, cancellationToken);
        if (fixture is null)
            return NotFound(new { Message = "Fixture not found." });

        var players = await _fixtureMatchCenterReadService.GetPlayersAsync(fixture.Id, cancellationToken);
        return Ok(players);
    }

    [HttpGet("{apiFixtureId:long}/match-center")]
    public async Task<IActionResult> GetMatchCenter(
        long apiFixtureId,
        [FromQuery] string? marketName,
        CancellationToken cancellationToken = default)
    {
        var fixture = await GetFixtureWithRelationsAsync(apiFixtureId, cancellationToken);
        if (fixture is null)
            return NotFound(new { Message = "Fixture not found." });

        var detail = await BuildFixtureDetailAsync(fixture, marketName, cancellationToken);
        var events = await _fixtureMatchCenterReadService.GetEventsAsync(fixture.Id, cancellationToken);
        var statistics = await _fixtureMatchCenterReadService.GetStatisticsAsync(fixture.Id, cancellationToken);
        var lineups = await _fixtureMatchCenterReadService.GetLineupsAsync(fixture.Id, cancellationToken);
        var players = await _fixtureMatchCenterReadService.GetPlayersAsync(fixture.Id, cancellationToken);

        return Ok(new FixtureMatchCenterDto
        {
            Detail = detail,
            Events = events,
            Statistics = statistics,
            Lineups = lineups,
            Players = players
        });
    }

    [HttpGet("{apiFixtureId:long}/predictions")]
    public async Task<IActionResult> GetFixturePredictions(
        long apiFixtureId,
        CancellationToken cancellationToken = default)
    {
        var fixture = await GetFixtureWithRelationsAsync(apiFixtureId, cancellationToken);
        if (fixture is null)
            return NotFound(new { Message = "Fixture not found." });

        var prediction = await _fixturePreviewReadService.GetPredictionAsync(fixture.Id, cancellationToken);
        return Ok(prediction);
    }

    [HttpGet("{apiFixtureId:long}/injuries")]
    public async Task<IActionResult> GetFixtureInjuries(
        long apiFixtureId,
        CancellationToken cancellationToken = default)
    {
        var fixture = await GetFixtureWithRelationsAsync(apiFixtureId, cancellationToken);
        if (fixture is null)
            return NotFound(new { Message = "Fixture not found." });

        var injuries = await _fixturePreviewReadService.GetInjuriesAsync(fixture.Id, cancellationToken);
        return Ok(injuries);
    }

    [HttpGet("{apiFixtureId:long}/head-to-head")]
    public async Task<IActionResult> GetHeadToHead(
        long apiFixtureId,
        CancellationToken cancellationToken = default)
    {
        var fixture = await GetFixtureWithRelationsAsync(apiFixtureId, cancellationToken);
        if (fixture is null)
            return NotFound(new { Message = "Fixture not found." });

        var h2h = await _fixturePreviewReadService.GetHeadToHeadAsync(fixture, cancellationToken: cancellationToken);
        return Ok(h2h);
    }

    [HttpGet("{apiFixtureId:long}/preview")]
    public async Task<IActionResult> GetPreview(
        long apiFixtureId,
        [FromQuery] string? marketName,
        CancellationToken cancellationToken = default)
    {
        var fixture = await GetFixtureWithRelationsAsync(apiFixtureId, cancellationToken);
        if (fixture is null)
            return NotFound(new { Message = "Fixture not found." });

        var detail = await BuildFixtureDetailAsync(fixture, marketName, cancellationToken);
        var prediction = await _fixturePreviewReadService.GetPredictionAsync(fixture.Id, cancellationToken);
        var injuries = await _fixturePreviewReadService.GetInjuriesAsync(fixture.Id, cancellationToken);
        var homeForm = await _fixturePreviewReadService.GetRecentFormAsync(fixture, true, cancellationToken: cancellationToken);
        var awayForm = await _fixturePreviewReadService.GetRecentFormAsync(fixture, false, cancellationToken: cancellationToken);
        var headToHead = await _fixturePreviewReadService.GetHeadToHeadAsync(fixture, cancellationToken: cancellationToken);

        return Ok(new FixturePreviewDto
        {
            Detail = detail,
            Prediction = prediction,
            Injuries = injuries,
            HomeRecentForm = homeForm,
            AwayRecentForm = awayForm,
            HeadToHead = headToHead
        });
    }

    [HttpGet("{apiFixtureId:long}/odds")]
    public async Task<IActionResult> GetFixtureOdds(
        long apiFixtureId,
        [FromQuery] string? marketName,
        [FromQuery] bool latestOnly = true,
        CancellationToken cancellationToken = default)
    {
        var fixture = await GetFixtureWithRelationsAsync(apiFixtureId, cancellationToken);
        if (fixture is null)
        {
            return NotFound(new
            {
                Message = "Fixture not found."
            });
        }

        var odds = await GetCurrentOddsAsync(
            fixture,
            marketName,
            latestOnly,
            cancellationToken);

        if (odds.Count == 0)
        {
            return NotFound(new
            {
                Message = "No odds found for this fixture."
            });
        }

        return Ok(odds);
    }

    [HttpGet("{apiFixtureId:long}/best-odds")]
    public async Task<IActionResult> GetFixtureBestOdds(
        long apiFixtureId,
        [FromQuery] string? marketName,
        CancellationToken cancellationToken = default)
    {
        var fixture = await GetFixtureWithRelationsAsync(apiFixtureId, cancellationToken);
        if (fixture is null)
        {
            return NotFound(new
            {
                Message = "Fixture not found."
            });
        }

        var bestOdds = await GetCurrentBestOddsAsync(
            fixture,
            marketName,
            cancellationToken);

        if (bestOdds is null)
        {
            return NotFound(new
            {
                Message = "No odds found for this fixture."
            });
        }

        return Ok(bestOdds);
    }

    [HttpGet("{apiFixtureId:long}/odds/live")]
    public async Task<IActionResult> GetFixtureLiveOdds(
        long apiFixtureId,
        [FromQuery] long? betId,
        [FromQuery] long? bookmakerId,
        [FromQuery] bool latestOnly = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _liveOddsService.GetLiveOddsAsync(
            apiFixtureId: apiFixtureId,
            betId: betId,
            bookmakerId: bookmakerId,
            latestOnly: latestOnly,
            cancellationToken: cancellationToken);

        if (result.Count == 0)
        {
            return NotFound(new
            {
                Message = "No live odds found for this fixture."
            });
        }

        return Ok(result);
    }

    [HttpGet("{apiFixtureId:long}/odds/history")]
    public async Task<IActionResult> GetFixtureOddsHistory(
        long apiFixtureId,
        [FromQuery] string? marketName,
        CancellationToken cancellationToken = default)
    {
        var history = await _oddsAnalyticsService.GetHistoryAsync(
            apiFixtureId,
            marketName,
            cancellationToken);

        if (history is null)
        {
            return NotFound(new
            {
                Message = "No odds history found for this fixture."
            });
        }

        return Ok(history);
    }

    [HttpGet("{apiFixtureId:long}/odds/movement")]
    public async Task<IActionResult> GetFixtureOddsMovement(
        long apiFixtureId,
        [FromQuery] string? marketName,
        CancellationToken cancellationToken = default)
    {
        var movement = await _oddsAnalyticsService.GetMovementAsync(
            apiFixtureId,
            marketName,
            cancellationToken);

        if (movement.Count == 0)
        {
            return NotFound(new
            {
                Message = "No odds movement analytics found for this fixture."
            });
        }

        return Ok(movement);
    }

    [HttpGet("{apiFixtureId:long}/odds/consensus")]
    public async Task<IActionResult> GetFixtureOddsConsensus(
        long apiFixtureId,
        [FromQuery] string? marketName,
        CancellationToken cancellationToken = default)
    {
        var consensus = await _oddsAnalyticsService.GetConsensusAsync(
            apiFixtureId,
            marketName,
            cancellationToken);

        if (consensus is null)
        {
            return NotFound(new
            {
                Message = "No odds consensus found for this fixture."
            });
        }

        return Ok(consensus);
    }

    [HttpGet("{apiFixtureId:long}/odds/value-signals")]
    public async Task<IActionResult> GetFixtureOddsValueSignals(
        long apiFixtureId,
        [FromQuery] string? marketName,
        CancellationToken cancellationToken = default)
    {
        var valueSignals = await _oddsAnalyticsService.GetValueSignalsAsync(
            apiFixtureId,
            marketName,
            cancellationToken);

        if (valueSignals is null)
        {
            return NotFound(new
            {
                Message = "No odds value signals found for this fixture."
            });
        }

        return Ok(valueSignals);
    }

    private IQueryable<Fixture> BuildFilteredQuery(
        long? leagueId,
        int? season,
        string? status,
        FixtureStateBucket? state,
        long? teamId,
        DateOnly? date,
        DateOnly? from,
        DateOnly? to)
    {
        var query = _dbContext.Fixtures
            .AsNoTracking()
            .Include(x => x.League)
                .ThenInclude(x => x.Country)
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            .AsQueryable();

        if (leagueId.HasValue)
        {
            query = query.Where(x => x.League.ApiLeagueId == leagueId.Value);
        }

        if (season.HasValue)
        {
            query = query.Where(x => x.Season == season.Value);
        }

        if (teamId.HasValue)
        {
            query = query.Where(x =>
                x.HomeTeam.ApiTeamId == teamId.Value ||
                x.AwayTeam.ApiTeamId == teamId.Value);
        }

        var normalizedStatus = FixtureStatusMapper.NormalizeShort(status);
        if (normalizedStatus is not null)
        {
            query = query.Where(x => x.Status == normalizedStatus);
        }

        if (state.HasValue)
        {
            if (state.Value == FixtureStateBucket.Other)
            {
                var knownStatuses = FixtureStatusMapper.GetKnownStatuses();
                query = query.Where(x => x.Status == null || !knownStatuses.Contains(x.Status));
            }
            else
            {
                var statuses = FixtureStatusMapper.GetStatusesForBucket(state.Value);
                query = query.Where(x => x.Status != null && statuses.Contains(x.Status));
            }
        }

        if (date.HasValue)
        {
            var startUtc = ToUtcStart(date.Value);
            var endUtc = ToUtcEndExclusive(date.Value);

            query = query.Where(x => x.KickoffAt >= startUtc && x.KickoffAt < endUtc);
        }
        else
        {
            if (from.HasValue)
            {
                var startUtc = ToUtcStart(from.Value);
                query = query.Where(x => x.KickoffAt >= startUtc);
            }

            if (to.HasValue)
            {
                var endUtc = ToUtcEndExclusive(to.Value);
                query = query.Where(x => x.KickoffAt < endUtc);
            }
        }

        return query;
    }

    private static IQueryable<Fixture> ApplySort(IQueryable<Fixture> query, string? direction)
    {
        var normalizedDirection = direction?.Trim().ToLowerInvariant();

        return normalizedDirection == "desc"
            ? query.OrderByDescending(x => x.KickoffAt).ThenByDescending(x => x.ApiFixtureId)
            : query.OrderBy(x => x.KickoffAt).ThenBy(x => x.ApiFixtureId);
    }

    private static string? ValidateDateFilters(DateOnly? date, DateOnly? from, DateOnly? to)
    {
        if (date.HasValue && (from.HasValue || to.HasValue))
            return "Use either 'date' or the 'from'/'to' range filters, not both.";

        if (from.HasValue && to.HasValue && from.Value > to.Value)
            return "'from' cannot be after 'to'.";

        return null;
    }

    private static DateTime ToUtcStart(DateOnly value)
    {
        return DateTime.SpecifyKind(value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    }

    private static DateTime ToUtcEndExclusive(DateOnly value)
    {
        return ToUtcStart(value.AddDays(1));
    }

    private static FixtureDto MapFixture(Fixture fixture)
    {
        return new FixtureDto
        {
            Id = fixture.Id,
            ApiFixtureId = fixture.ApiFixtureId,
            Season = fixture.Season,
            KickoffAt = fixture.KickoffAt,
            Status = fixture.Status,
            StatusLong = fixture.StatusLong,
            Elapsed = fixture.Elapsed,
            StatusExtra = fixture.StatusExtra,
            StateBucket = FixtureStatusMapper.GetStateBucket(fixture.Status),
            Referee = fixture.Referee,
            Timezone = fixture.Timezone,
            VenueName = fixture.VenueName,
            VenueCity = fixture.VenueCity,
            Round = fixture.Round,
            LeagueId = fixture.League.ApiLeagueId,
            LeagueApiId = fixture.League.ApiLeagueId,
            LeagueName = fixture.League.Name,
            CountryName = fixture.League.Country.Name,
            HomeTeamId = fixture.HomeTeamId,
            HomeTeamApiId = fixture.HomeTeam.ApiTeamId,
            HomeTeamName = fixture.HomeTeam.Name,
            HomeTeamLogoUrl = fixture.HomeTeam.LogoUrl,
            AwayTeamId = fixture.AwayTeamId,
            AwayTeamApiId = fixture.AwayTeam.ApiTeamId,
            AwayTeamName = fixture.AwayTeam.Name,
            AwayTeamLogoUrl = fixture.AwayTeam.LogoUrl,
            HomeGoals = fixture.HomeGoals,
            AwayGoals = fixture.AwayGoals
        };
    }

    private async Task<Fixture?> GetFixtureWithRelationsAsync(long apiFixtureId, CancellationToken cancellationToken)
    {
        return await _dbContext.Fixtures
            .AsNoTracking()
            .Include(x => x.League)
                .ThenInclude(x => x.Country)
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            .FirstOrDefaultAsync(x => x.ApiFixtureId == apiFixtureId, cancellationToken);
    }

    private async Task<FixtureDetailDto> BuildFixtureDetailAsync(
        Fixture fixture,
        string? marketName,
        CancellationToken cancellationToken)
    {
        var syncStates = await _dbContext.SyncStates
            .AsNoTracking()
            .Where(x =>
                x.LeagueApiId == fixture.League.ApiLeagueId &&
                x.Season == fixture.Season &&
                (x.EntityType == "fixtures_upcoming" ||
                 x.EntityType == "fixtures_live" ||
                 x.EntityType == "fixtures_full" ||
                 x.EntityType == "odds"))
            .ToListAsync(cancellationToken);

        DateTime? FindSyncValue(string entityType)
        {
            return syncStates
                .Where(x => x.EntityType == entityType)
                .Select(x => (DateTime?)x.LastSyncedAt)
                .OrderByDescending(x => x)
                .FirstOrDefault();
        }

        var bestOdds = await GetCurrentBestOddsAsync(fixture, marketName, cancellationToken);
        var latestOddsCollectedAtUtc = await GetCurrentOddsCollectedAtUtcAsync(fixture, marketName, cancellationToken);

        return new FixtureDetailDto
        {
            Fixture = MapFixture(fixture),
            BestOdds = bestOdds,
            LatestOddsCollectedAtUtc = latestOddsCollectedAtUtc,
            Freshness = new FixtureFreshnessDto
            {
                LastLiveStatusSyncedAtUtc = fixture.LastLiveStatusSyncedAtUtc,
                LastEventSyncedAtUtc = fixture.LastEventSyncedAtUtc,
                LastStatisticsSyncedAtUtc = fixture.LastStatisticsSyncedAtUtc,
                LastLineupsSyncedAtUtc = fixture.LastLineupsSyncedAtUtc,
                LastPlayerStatisticsSyncedAtUtc = fixture.LastPlayerStatisticsSyncedAtUtc,
                LastPredictionSyncedAtUtc = fixture.LastPredictionSyncedAtUtc,
                LastInjuriesSyncedAtUtc = fixture.LastInjuriesSyncedAtUtc
            },
            FixturesLiveLastSyncedAtUtc = FindSyncValue("fixtures_live"),
            FixturesUpcomingLastSyncedAtUtc = FindSyncValue("fixtures_upcoming"),
            FixturesFullLastSyncedAtUtc = FindSyncValue("fixtures_full"),
            OddsLastSyncedAtUtc = FindSyncValue("odds")
        };
    }

    private async Task<IReadOnlyList<OddDto>> GetCurrentOddsAsync(
        Fixture fixture,
        string? marketName,
        bool latestOnly,
        CancellationToken cancellationToken)
    {
        if (ShouldPreferLiveOdds(fixture, marketName, latestOnly))
        {
            var liveOdds = await _liveOddsService.GetMatchWinnerOddsAsync(
                apiFixtureId: fixture.ApiFixtureId,
                latestOnly: true,
                cancellationToken: cancellationToken);

            if (liveOdds.Count > 0)
                return liveOdds;
        }

        return await _preMatchOddsService.GetFixtureOddsAsync(
            apiFixtureId: fixture.ApiFixtureId,
            marketName: marketName,
            latestOnly: latestOnly,
            cancellationToken: cancellationToken);
    }

    private async Task<BestOddsDto?> GetCurrentBestOddsAsync(
        Fixture fixture,
        string? marketName,
        CancellationToken cancellationToken)
    {
        if (ShouldPreferLiveOdds(fixture, marketName, latestOnly: true))
        {
            var liveBestOdds = await _liveOddsService.GetBestMatchWinnerOddsAsync(
                apiFixtureId: fixture.ApiFixtureId,
                cancellationToken: cancellationToken);

            if (liveBestOdds is not null)
                return liveBestOdds;
        }

        return await _preMatchOddsService.GetBestOddsAsync(
            apiFixtureId: fixture.ApiFixtureId,
            marketName: marketName,
            cancellationToken: cancellationToken);
    }

    private async Task<DateTime?> GetCurrentOddsCollectedAtUtcAsync(
        Fixture fixture,
        string? marketName,
        CancellationToken cancellationToken)
    {
        if (ShouldPreferLiveOdds(fixture, marketName, latestOnly: true))
        {
            var liveCollectedAtUtc = await _liveOddsService.GetLatestMatchWinnerCollectedAtUtcAsync(
                apiFixtureId: fixture.ApiFixtureId,
                cancellationToken: cancellationToken);

            if (liveCollectedAtUtc.HasValue)
                return liveCollectedAtUtc;
        }

        return await _preMatchOddsService.GetLatestCollectedAtUtcAsync(
            apiFixtureId: fixture.ApiFixtureId,
            marketName: marketName,
            cancellationToken: cancellationToken);
    }

    private static bool ShouldPreferLiveOdds(Fixture fixture, string? marketName, bool latestOnly)
    {
        if (!latestOnly)
            return false;

        if (!string.IsNullOrWhiteSpace(marketName) &&
            !string.Equals(marketName.Trim(), PreMatchOddsService.DefaultMarketName, StringComparison.OrdinalIgnoreCase))
            return false;

        return FixtureStatusMapper.GetStateBucket(fixture.Status) == FixtureStateBucket.Live;
    }
}
