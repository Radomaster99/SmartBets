using Microsoft.Extensions.Options;

namespace SmartBets.Services;

public class DataRetentionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<DataRetentionOptions> _optionsMonitor;
    private readonly ILogger<DataRetentionBackgroundService> _logger;

    public DataRetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<DataRetentionOptions> optionsMonitor,
        ILogger<DataRetentionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data retention background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;
            var delay = options.GetInterval();

            if (!options.Enabled)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var retentionService = scope.ServiceProvider.GetRequiredService<DataRetentionService>();
                var result = await retentionService.CleanupAsync(stoppingToken);

                _logger.LogInformation(
                    "Data retention cleanup completed. SyncErrors={SyncErrors}, NormalizedMatchWinnerLiveOdds={NormalizedMatchWinnerLiveOdds}, DeletedNonMatchWinnerLiveOdds={DeletedNonMatchWinnerLiveOdds}, LiveOdds={LiveOdds}, TheOddsLiveOdds={TheOddsLiveOdds}, PreMatchOdds={PreMatchOdds}, OddsOpenClose={OddsOpenClose}, OddsMovements={OddsMovements}, MarketConsensus={MarketConsensus}",
                    result.DeletedSyncErrors,
                    result.NormalizedLegacyMatchWinnerLiveOdds,
                    result.DeletedNonMatchWinnerLiveOdds,
                    result.DeletedLiveOdds,
                    result.DeletedTheOddsLiveOdds,
                    result.DeletedPreMatchOdds,
                    result.DeletedOddsOpenClose,
                    result.DeletedOddsMovements,
                    result.DeletedMarketConsensuses);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data retention cleanup cycle failed.");

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var syncErrorService = scope.ServiceProvider.GetRequiredService<SyncErrorService>();
                    await syncErrorService.RecordAsync(
                        "data_retention",
                        "cleanup",
                        "background",
                        ex.Message,
                        cancellationToken: stoppingToken);
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Failed to persist data retention cleanup error.");
                }

                delay = options.GetErrorRetryInterval();
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

        _logger.LogInformation("Data retention background service stopped.");
    }
}
