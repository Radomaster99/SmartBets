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
    private readonly PreMatchOddsService _preMatchOddsService;
    private readonly AppDbContext _dbContext;
    private readonly SyncStateService _syncStateService;

    public FixturesController(
        FixtureSyncService fixtureSyncService,
        PreMatchOddsService preMatchOddsService,
        AppDbContext dbContext,
        SyncStateService syncStateService)
    {
        _fixtureSyncService = fixtureSyncService ?? throw new ArgumentNullException(nameof(fixtureSyncService));
        _preMatchOddsService = preMatchOddsService ?? throw new ArgumentNullException(nameof(preMatchOddsService));
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
        var fixture = await _dbContext.Fixtures
            .AsNoTracking()
            .Include(x => x.League)
                .ThenInclude(x => x.Country)
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            .FirstOrDefaultAsync(x => x.ApiFixtureId == apiFixtureId, cancellationToken);

        if (fixture is null)
        {
            return NotFound(new
            {
                Message = "Fixture not found."
            });
        }

        var syncStates = await _dbContext.SyncStates
            .AsNoTracking()
            .Where(x =>
                x.LeagueApiId == fixture.League.ApiLeagueId &&
                x.Season == fixture.Season &&
                (x.EntityType == "fixtures_upcoming" ||
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

        var detail = new FixtureDetailDto
        {
            Fixture = MapFixture(fixture),
            BestOdds = await _preMatchOddsService.GetBestOddsAsync(
                apiFixtureId: apiFixtureId,
                marketName: marketName,
                cancellationToken: cancellationToken),
            LatestOddsCollectedAtUtc = await _preMatchOddsService.GetLatestCollectedAtUtcAsync(
                apiFixtureId: apiFixtureId,
                marketName: marketName,
                cancellationToken: cancellationToken),
            FixturesUpcomingLastSyncedAtUtc = FindSyncValue("fixtures_upcoming"),
            FixturesFullLastSyncedAtUtc = FindSyncValue("fixtures_full"),
            OddsLastSyncedAtUtc = FindSyncValue("odds")
        };

        return Ok(detail);
    }

    [HttpGet("{apiFixtureId:long}/odds")]
    public async Task<IActionResult> GetFixtureOdds(
        long apiFixtureId,
        [FromQuery] string? marketName,
        [FromQuery] bool latestOnly = true,
        CancellationToken cancellationToken = default)
    {
        var odds = await _preMatchOddsService.GetFixtureOddsAsync(
            apiFixtureId: apiFixtureId,
            marketName: marketName,
            latestOnly: latestOnly,
            cancellationToken: cancellationToken);

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
        var bestOdds = await _preMatchOddsService.GetBestOddsAsync(
            apiFixtureId: apiFixtureId,
            marketName: marketName,
            cancellationToken: cancellationToken);

        if (bestOdds is null)
        {
            return NotFound(new
            {
                Message = "No odds found for this fixture."
            });
        }

        return Ok(bestOdds);
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
            StateBucket = FixtureStatusMapper.GetStateBucket(fixture.Status),
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
}
