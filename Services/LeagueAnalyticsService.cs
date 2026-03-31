using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Entities;
using SmartBets.Models.ApiFootball;

namespace SmartBets.Services;

public class LeagueAnalyticsService
{
    private static readonly TimeSpan DailyInterval = TimeSpan.FromHours(20);

    private readonly AppDbContext _dbContext;
    private readonly FootballApiService _apiService;
    private readonly LeagueCoverageService _leagueCoverageService;
    private readonly SyncStateService _syncStateService;

    public LeagueAnalyticsService(
        AppDbContext dbContext,
        FootballApiService apiService,
        LeagueCoverageService leagueCoverageService,
        SyncStateService syncStateService)
    {
        _dbContext = dbContext;
        _apiService = apiService;
        _leagueCoverageService = leagueCoverageService;
        _syncStateService = syncStateService;
    }

    public async Task<List<LeagueRoundDto>> GetRoundsAsync(
        long apiLeagueId,
        int season,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.LeagueRounds
            .AsNoTracking()
            .Where(x => x.League.ApiLeagueId == apiLeagueId && x.Season == season)
            .OrderBy(x => x.SortOrder)
            .Select(x => new LeagueRoundDto
            {
                RoundName = x.RoundName,
                SortOrder = x.SortOrder,
                IsCurrent = x.IsCurrent,
                SyncedAtUtc = x.SyncedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<LeagueRoundDto?> GetCurrentRoundAsync(
        long apiLeagueId,
        int season,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.LeagueRounds
            .AsNoTracking()
            .Where(x => x.League.ApiLeagueId == apiLeagueId && x.Season == season && x.IsCurrent)
            .OrderBy(x => x.SortOrder)
            .Select(x => new LeagueRoundDto
            {
                RoundName = x.RoundName,
                SortOrder = x.SortOrder,
                IsCurrent = x.IsCurrent,
                SyncedAtUtc = x.SyncedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<LeagueTopPlayerDto>> GetTopScorersAsync(
        long apiLeagueId,
        int season,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.LeagueTopScorers
            .AsNoTracking()
            .Where(x => x.League.ApiLeagueId == apiLeagueId && x.Season == season)
            .OrderBy(x => x.Rank)
            .Select(x => new LeagueTopPlayerDto
            {
                Rank = x.Rank,
                ApiPlayerId = x.ApiPlayerId,
                PlayerName = x.PlayerName,
                PlayerPhotoUrl = x.PlayerPhotoUrl,
                TeamApiId = x.TeamApiId,
                TeamName = x.TeamName,
                TeamLogoUrl = x.TeamLogoUrl,
                Appearances = x.Appearances,
                Minutes = x.Minutes,
                Position = x.Position,
                Rating = x.Rating,
                Goals = x.Goals,
                Assists = x.Assists,
                ShotsTotal = x.ShotsTotal,
                ShotsOn = x.ShotsOn,
                SyncedAtUtc = x.SyncedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public Task<List<LeagueTopPlayerDto>> GetTopAssistsAsync(
        long apiLeagueId,
        int season,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.LeagueTopAssists
            .AsNoTracking()
            .Where(x => x.League.ApiLeagueId == apiLeagueId && x.Season == season)
            .OrderBy(x => x.Rank)
            .Select(x => new LeagueTopPlayerDto
            {
                Rank = x.Rank,
                ApiPlayerId = x.ApiPlayerId,
                PlayerName = x.PlayerName,
                PlayerPhotoUrl = x.PlayerPhotoUrl,
                TeamApiId = x.TeamApiId,
                TeamName = x.TeamName,
                TeamLogoUrl = x.TeamLogoUrl,
                Appearances = x.Appearances,
                Minutes = x.Minutes,
                Position = x.Position,
                Rating = x.Rating,
                Goals = x.Goals,
                Assists = x.Assists,
                PassesKey = x.PassesKey,
                ChancesCreated = x.ChancesCreated,
                SyncedAtUtc = x.SyncedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public Task<List<LeagueTopPlayerDto>> GetTopCardsAsync(
        long apiLeagueId,
        int season,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.LeagueTopCards
            .AsNoTracking()
            .Where(x => x.League.ApiLeagueId == apiLeagueId && x.Season == season)
            .OrderBy(x => x.CombinedRank)
            .Select(x => new LeagueTopPlayerDto
            {
                Rank = x.CombinedRank,
                ApiPlayerId = x.ApiPlayerId,
                PlayerName = x.PlayerName,
                PlayerPhotoUrl = x.PlayerPhotoUrl,
                TeamApiId = x.TeamApiId,
                TeamName = x.TeamName,
                TeamLogoUrl = x.TeamLogoUrl,
                Appearances = x.Appearances,
                Minutes = x.Minutes,
                Position = x.Position,
                Rating = x.Rating,
                YellowCards = x.YellowCards,
                RedCards = x.RedCards,
                SyncedAtUtc = x.SyncedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<LeagueDashboardDto?> GetDashboardAsync(
        long apiLeagueId,
        int season,
        CancellationToken cancellationToken = default)
    {
        var league = await _dbContext.Leagues
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ApiLeagueId == apiLeagueId && x.Season == season, cancellationToken);

        if (league is null)
            return null;

        var rounds = await GetRoundsAsync(apiLeagueId, season, cancellationToken);
        var currentRound = rounds.FirstOrDefault(x => x.IsCurrent)?.RoundName;

        return new LeagueDashboardDto
        {
            LeagueApiId = apiLeagueId,
            LeagueName = league.Name,
            Season = season,
            CurrentRound = currentRound,
            Rounds = rounds,
            TopScorers = await GetTopScorersAsync(apiLeagueId, season, cancellationToken),
            TopAssists = await GetTopAssistsAsync(apiLeagueId, season, cancellationToken),
            TopCards = await GetTopCardsAsync(apiLeagueId, season, cancellationToken)
        };
    }

    public async Task<LeagueAnalyticsSyncResultDto> SyncLeagueAnalyticsAsync(
        long apiLeagueId,
        int season,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var league = await _dbContext.Leagues
            .FirstOrDefaultAsync(x => x.ApiLeagueId == apiLeagueId && x.Season == season, cancellationToken);

        if (league is null)
            throw new InvalidOperationException($"League {apiLeagueId} season {season} was not found.");

        var coverage = await _leagueCoverageService.GetCoverageAsync(apiLeagueId, season, cancellationToken);
        var skipped = new List<string>();

        var roundsSynced = await SyncRoundsAsync(league, coverage, force, nowUtc, skipped, cancellationToken);
        var topScorersSynced = await SyncTopScorersAsync(league, coverage, force, nowUtc, skipped, cancellationToken);
        var topAssistsSynced = await SyncTopAssistsAsync(league, coverage, force, nowUtc, skipped, cancellationToken);
        var topCardsSynced = await SyncTopCardsAsync(league, coverage, force, nowUtc, skipped, cancellationToken);

        return new LeagueAnalyticsSyncResultDto
        {
            LeagueApiId = apiLeagueId,
            Season = season,
            Forced = force,
            RoundsSynced = roundsSynced,
            TopScorersSynced = topScorersSynced,
            TopAssistsSynced = topAssistsSynced,
            TopCardsSynced = topCardsSynced,
            SkippedComponents = skipped,
            ExecutedAtUtc = nowUtc
        };
    }

    public async Task<LeagueAnalyticsBatchSyncResultDto> SyncSupportedLeaguesAsync(
        int? season = null,
        bool activeOnly = true,
        int maxLeagues = 10,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        maxLeagues = Math.Clamp(maxLeagues, 1, 25);

        var query = _dbContext.SupportedLeagues
            .AsNoTracking()
            .AsQueryable();

        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        if (season.HasValue)
        {
            query = query.Where(x => x.Season == season.Value);
        }

        var supportedLeagues = await query
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.LeagueApiId)
            .Take(maxLeagues)
            .ToListAsync(cancellationToken);

        var items = new List<LeagueAnalyticsSyncResultDto>();

        foreach (var supportedLeague in supportedLeagues)
        {
            items.Add(await SyncLeagueAnalyticsAsync(
                supportedLeague.LeagueApiId,
                supportedLeague.Season,
                force,
                cancellationToken));
        }

        return new LeagueAnalyticsBatchSyncResultDto
        {
            LeaguesConsidered = supportedLeagues.Count,
            LeaguesSynced = items.Count(x => x.RoundsSynced || x.TopScorersSynced || x.TopAssistsSynced || x.TopCardsSynced),
            Forced = force,
            ExecutedAtUtc = DateTime.UtcNow,
            Items = items
        };
    }

    private async Task<bool> SyncRoundsAsync(
        League league,
        LeagueSeasonCoverage? coverage,
        bool force,
        DateTime nowUtc,
        List<string> skipped,
        CancellationToken cancellationToken)
    {
        if (coverage is not null && !coverage.HasFixtures)
        {
            skipped.Add("rounds:coverage_disabled");
            return false;
        }

        if (!force && await IsRecentlySyncedAsync("league_rounds", league.ApiLeagueId, league.Season, nowUtc, cancellationToken))
        {
            skipped.Add("rounds:skipped_fresh");
            return false;
        }

        var rounds = await _apiService.GetRoundsAsync(league.ApiLeagueId, league.Season, false, cancellationToken);
        var currentRounds = await _apiService.GetRoundsAsync(league.ApiLeagueId, league.Season, true, cancellationToken);
        var current = currentRounds.FirstOrDefault();

        await _dbContext.LeagueRounds
            .Where(x => x.LeagueId == league.Id && x.Season == league.Season)
            .ExecuteDeleteAsync(cancellationToken);

        var rows = rounds.Select((round, index) => new LeagueRound
        {
            LeagueId = league.Id,
            Season = league.Season,
            RoundName = round,
            SortOrder = index,
            IsCurrent = string.Equals(round, current, StringComparison.OrdinalIgnoreCase),
            SyncedAtUtc = nowUtc
        }).ToList();

        if (rows.Count > 0)
        {
            _dbContext.LeagueRounds.AddRange(rows);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _syncStateService.SetLastSyncedAtAsync("league_rounds", league.ApiLeagueId, league.Season, nowUtc, cancellationToken);
        return true;
    }

    private async Task<bool> SyncTopScorersAsync(
        League league,
        LeagueSeasonCoverage? coverage,
        bool force,
        DateTime nowUtc,
        List<string> skipped,
        CancellationToken cancellationToken)
    {
        if (coverage is not null && !coverage.HasTopScorers)
        {
            skipped.Add("top_scorers:coverage_disabled");
            return false;
        }

        if (!force && await IsRecentlySyncedAsync("league_top_scorers", league.ApiLeagueId, league.Season, nowUtc, cancellationToken))
        {
            skipped.Add("top_scorers:skipped_fresh");
            return false;
        }

        var source = await _apiService.GetTopScorersAsync(league.ApiLeagueId, league.Season, cancellationToken);

        await _dbContext.LeagueTopScorers
            .Where(x => x.LeagueId == league.Id && x.Season == league.Season)
            .ExecuteDeleteAsync(cancellationToken);

        var rows = source.Select((item, index) => MapTopScorerEntity(league, item, index + 1, nowUtc)).ToList();
        if (rows.Count > 0)
        {
            _dbContext.LeagueTopScorers.AddRange(rows);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _syncStateService.SetLastSyncedAtAsync("league_top_scorers", league.ApiLeagueId, league.Season, nowUtc, cancellationToken);
        return true;
    }

    private async Task<bool> SyncTopAssistsAsync(
        League league,
        LeagueSeasonCoverage? coverage,
        bool force,
        DateTime nowUtc,
        List<string> skipped,
        CancellationToken cancellationToken)
    {
        if (coverage is not null && !coverage.HasTopAssists)
        {
            skipped.Add("top_assists:coverage_disabled");
            return false;
        }

        if (!force && await IsRecentlySyncedAsync("league_top_assists", league.ApiLeagueId, league.Season, nowUtc, cancellationToken))
        {
            skipped.Add("top_assists:skipped_fresh");
            return false;
        }

        var source = await _apiService.GetTopAssistsAsync(league.ApiLeagueId, league.Season, cancellationToken);

        await _dbContext.LeagueTopAssists
            .Where(x => x.LeagueId == league.Id && x.Season == league.Season)
            .ExecuteDeleteAsync(cancellationToken);

        var rows = source.Select((item, index) => MapTopAssistEntity(league, item, index + 1, nowUtc)).ToList();
        if (rows.Count > 0)
        {
            _dbContext.LeagueTopAssists.AddRange(rows);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _syncStateService.SetLastSyncedAtAsync("league_top_assists", league.ApiLeagueId, league.Season, nowUtc, cancellationToken);
        return true;
    }

    private async Task<bool> SyncTopCardsAsync(
        League league,
        LeagueSeasonCoverage? coverage,
        bool force,
        DateTime nowUtc,
        List<string> skipped,
        CancellationToken cancellationToken)
    {
        if (coverage is not null && !coverage.HasTopCards)
        {
            skipped.Add("top_cards:coverage_disabled");
            return false;
        }

        if (!force && await IsRecentlySyncedAsync("league_top_cards", league.ApiLeagueId, league.Season, nowUtc, cancellationToken))
        {
            skipped.Add("top_cards:skipped_fresh");
            return false;
        }

        var yellow = await _apiService.GetTopYellowCardsAsync(league.ApiLeagueId, league.Season, cancellationToken);
        var red = await _apiService.GetTopRedCardsAsync(league.ApiLeagueId, league.Season, cancellationToken);

        await _dbContext.LeagueTopCards
            .Where(x => x.LeagueId == league.Id && x.Season == league.Season)
            .ExecuteDeleteAsync(cancellationToken);

        var merged = new Dictionary<string, LeagueTopCard>(StringComparer.Ordinal);

        foreach (var item in yellow.Select((value, index) => new { value, index }))
        {
            var entity = MapTopCardBase(league, item.value, nowUtc);
            entity.YellowRank = item.index + 1;
            entity.YellowCards = GetFirstStats(item.value).Cards.Yellow ?? 0;
            merged[$"{entity.ApiPlayerId}:{entity.TeamApiId}"] = entity;
        }

        foreach (var item in red.Select((value, index) => new { value, index }))
        {
            var baseEntity = MapTopCardBase(league, item.value, nowUtc);
            var key = $"{baseEntity.ApiPlayerId}:{baseEntity.TeamApiId}";

            if (!merged.TryGetValue(key, out var entity))
            {
                entity = baseEntity;
                merged[key] = entity;
            }

            entity.RedRank = item.index + 1;
            entity.RedCards = GetFirstStats(item.value).Cards.Red ?? 0;
            entity.SyncedAtUtc = nowUtc;
        }

        var rows = merged.Values
            .OrderByDescending(x => x.RedCards + x.YellowCards)
            .ThenByDescending(x => x.RedCards)
            .ThenByDescending(x => x.YellowCards)
            .ThenBy(x => x.PlayerName)
            .ToList();

        for (var index = 0; index < rows.Count; index++)
        {
            rows[index].CombinedRank = index + 1;
        }

        if (rows.Count > 0)
        {
            _dbContext.LeagueTopCards.AddRange(rows);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _syncStateService.SetLastSyncedAtAsync("league_top_cards", league.ApiLeagueId, league.Season, nowUtc, cancellationToken);
        return true;
    }

    private async Task<bool> IsRecentlySyncedAsync(
        string entityType,
        long leagueApiId,
        int season,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var lastSyncedAt = await _dbContext.SyncStates
            .AsNoTracking()
            .Where(x => x.EntityType == entityType && x.LeagueApiId == leagueApiId && x.Season == season)
            .Select(x => (DateTime?)x.LastSyncedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return lastSyncedAt.HasValue && nowUtc - lastSyncedAt.Value < DailyInterval;
    }

    private LeagueTopScorer MapTopScorerEntity(League league, ApiFootballTopPlayerItem item, int rank, DateTime syncedAtUtc)
    {
        var stats = GetFirstStats(item);

        return new LeagueTopScorer
        {
            LeagueId = league.Id,
            Season = league.Season,
            Rank = rank,
            ApiPlayerId = item.Player.Id,
            PlayerName = item.Player.Name,
            PlayerPhotoUrl = item.Player.Photo,
            TeamId = ResolveTeamId(stats.Team.Id),
            TeamApiId = stats.Team.Id,
            TeamName = stats.Team.Name,
            TeamLogoUrl = stats.Team.Logo,
            Appearances = stats.Games.Appearances,
            Minutes = stats.Games.Minutes,
            Position = stats.Games.Position,
            Rating = ParseDecimal(stats.Games.Rating),
            Goals = stats.Goals.Total,
            Assists = stats.Goals.Assists,
            ShotsTotal = stats.Shots.Total,
            ShotsOn = stats.Shots.On,
            PenaltiesScored = stats.Penalty.Scored,
            SyncedAtUtc = syncedAtUtc
        };
    }

    private LeagueTopAssist MapTopAssistEntity(League league, ApiFootballTopPlayerItem item, int rank, DateTime syncedAtUtc)
    {
        var stats = GetFirstStats(item);

        return new LeagueTopAssist
        {
            LeagueId = league.Id,
            Season = league.Season,
            Rank = rank,
            ApiPlayerId = item.Player.Id,
            PlayerName = item.Player.Name,
            PlayerPhotoUrl = item.Player.Photo,
            TeamId = ResolveTeamId(stats.Team.Id),
            TeamApiId = stats.Team.Id,
            TeamName = stats.Team.Name,
            TeamLogoUrl = stats.Team.Logo,
            Appearances = stats.Games.Appearances,
            Minutes = stats.Games.Minutes,
            Position = stats.Games.Position,
            Rating = ParseDecimal(stats.Games.Rating),
            Goals = stats.Goals.Total,
            Assists = stats.Goals.Assists,
            PassesKey = stats.Passes.Key,
            ChancesCreated = stats.Passes.Key,
            SyncedAtUtc = syncedAtUtc
        };
    }

    private LeagueTopCard MapTopCardBase(League league, ApiFootballTopPlayerItem item, DateTime syncedAtUtc)
    {
        var stats = GetFirstStats(item);

        return new LeagueTopCard
        {
            LeagueId = league.Id,
            Season = league.Season,
            ApiPlayerId = item.Player.Id,
            PlayerName = item.Player.Name,
            PlayerPhotoUrl = item.Player.Photo,
            TeamId = ResolveTeamId(stats.Team.Id),
            TeamApiId = stats.Team.Id,
            TeamName = stats.Team.Name,
            TeamLogoUrl = stats.Team.Logo,
            Appearances = stats.Games.Appearances,
            Minutes = stats.Games.Minutes,
            Position = stats.Games.Position,
            Rating = ParseDecimal(stats.Games.Rating),
            YellowCards = stats.Cards.Yellow ?? 0,
            RedCards = stats.Cards.Red ?? 0,
            SyncedAtUtc = syncedAtUtc
        };
    }

    private static ApiFootballTopPlayerStatisticsBlock GetFirstStats(ApiFootballTopPlayerItem item)
    {
        return item.Statistics.FirstOrDefault() ?? new ApiFootballTopPlayerStatisticsBlock();
    }

    private long? ResolveTeamId(long teamApiId)
    {
        return _dbContext.Teams
            .AsNoTracking()
            .Where(x => x.ApiTeamId == teamApiId)
            .Select(x => (long?)x.Id)
            .FirstOrDefault();
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
