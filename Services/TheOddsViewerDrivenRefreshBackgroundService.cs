using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartBets.Data;
using SmartBets.Enums;

namespace SmartBets.Services;

public class TheOddsViewerDrivenRefreshBackgroundService : BackgroundService
{
    private static readonly HashSet<string> LiveStatuses = FixtureStatusMapper
        .GetStatusesForBucket(FixtureStateBucket.Live)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TheOddsViewerActivityService _viewerActivityService;
    private readonly IOptionsMonitor<TheOddsApiOptions> _optionsMonitor;
    private readonly ILogger<TheOddsViewerDrivenRefreshBackgroundService> _logger;

    public TheOddsViewerDrivenRefreshBackgroundService(
        IServiceScopeFactory scopeFactory,
        TheOddsViewerActivityService viewerActivityService,
        IOptionsMonitor<TheOddsApiOptions> optionsMonitor,
        ILogger<TheOddsViewerDrivenRefreshBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _viewerActivityService = viewerActivityService;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("The Odds viewer-driven refresh background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;
            var delay = options.GetViewerRefreshInterval();

            try
            {
                if (options.Enabled && options.EnableViewerDrivenRefresh && options.IsConfigured())
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var theOddsLiveOddsService = scope.ServiceProvider.GetRequiredService<TheOddsLiveOddsService>();

                    var maxFixturesPerCycle = options.GetMaxViewerFixturesPerCycle();
                    var activeFixtureIds = _viewerActivityService.GetActiveFixtureIds(maxFixturesPerCycle);
                    if (activeFixtureIds.Count > 0)
                    {
                        var priorityFixtureIds = await GetPriorityKeepaliveFixtureIdsAsync(
                            dbContext,
                            activeFixtureIds,
                            options.GetPriorityKeepaliveCount(),
                            stoppingToken);

                        var targetFixtureIds = activeFixtureIds
                            .Concat(priorityFixtureIds)
                            .Distinct()
                            .Take(maxFixturesPerCycle)
                            .ToList();

                        if (targetFixtureIds.Count > 0)
                        {
                            var result = await theOddsLiveOddsService.SyncFixturesLiveOddsAsync(
                                targetFixtureIds,
                                force: false,
                                cancellationToken: stoppingToken);

                            _logger.LogInformation(
                                "The Odds viewer-driven refresh completed. ActiveFixtures={ActiveFixtures}, PriorityFixtures={PriorityFixtures}, TargetFixtures={TargetFixtures}, RequestsUsed={RequestsUsed}, FixturesMatched={FixturesMatched}, SnapshotsInserted={SnapshotsInserted}, SnapshotsSkippedUnchanged={SnapshotsSkippedUnchanged}, LeaguesSkippedRecentlySynced={LeaguesSkippedRecentlySynced}",
                                activeFixtureIds.Count,
                                priorityFixtureIds.Count,
                                targetFixtureIds.Count,
                                result.RequestsUsed,
                                result.FixturesMatched,
                                result.SnapshotsInserted,
                                result.SnapshotsSkippedUnchanged,
                                result.LeaguesSkippedRecentlySynced);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The Odds viewer-driven refresh cycle failed.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("The Odds viewer-driven refresh background service stopped.");
    }

    private static async Task<IReadOnlyList<long>> GetPriorityKeepaliveFixtureIdsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<long> excludedFixtureIds,
        int maxCount,
        CancellationToken cancellationToken)
    {
        if (maxCount <= 0)
            return Array.Empty<long>();

        return await dbContext.Fixtures
            .AsNoTracking()
            .Where(x => x.Status != null && LiveStatuses.Contains(x.Status))
            .Where(x => !excludedFixtureIds.Contains(x.ApiFixtureId))
            .Select(x => new
            {
                x.ApiFixtureId,
                x.Elapsed,
                x.KickoffAt,
                SupportedLeague = dbContext.SupportedLeagues
                    .Where(s => s.LeagueApiId == x.League.ApiLeagueId && s.Season == x.Season)
                    .Select(s => new { s.IsActive, s.Priority })
                    .FirstOrDefault()
            })
            .OrderByDescending(x => x.SupportedLeague != null && x.SupportedLeague.IsActive)
            .ThenBy(x => x.SupportedLeague != null ? x.SupportedLeague.Priority : int.MaxValue)
            .ThenByDescending(x => x.Elapsed ?? 0)
            .ThenByDescending(x => x.KickoffAt)
            .Take(maxCount)
            .Select(x => x.ApiFixtureId)
            .ToListAsync(cancellationToken);
    }
}
