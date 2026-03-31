using Microsoft.Extensions.Options;

namespace SmartBets.Services;

public class LiveAutomationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<LiveAutomationOptions> _optionsMonitor;
    private readonly ILogger<LiveAutomationBackgroundService> _logger;
    private readonly LiveAutomationWorkerState _state = new();

    public LiveAutomationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<LiveAutomationOptions> optionsMonitor,
        ILogger<LiveAutomationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Live automation background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = _optionsMonitor.CurrentValue.GetIdleInterval();

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<LiveAutomationOrchestrator>();
                var cycleResult = await orchestrator.RunCycleAsync(_state, stoppingToken);
                delay = cycleResult.NextDelay;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Live automation cycle failed.");
                delay = _optionsMonitor.CurrentValue.GetErrorRetryInterval();

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var syncErrorService = scope.ServiceProvider.GetRequiredService<SyncErrorService>();
                    await syncErrorService.RecordAsync(
                        "live_automation",
                        "cycle",
                        "background",
                        ex.Message,
                        cancellationToken: stoppingToken);
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Failed to persist live automation background error.");
                }
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

        _logger.LogInformation("Live automation background service stopped.");
    }
}
