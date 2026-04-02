using Microsoft.Extensions.Options;

namespace SmartBets.Services;

public class CoreDataAutomationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<CoreDataAutomationOptions> _optionsMonitor;
    private readonly ILogger<CoreDataAutomationBackgroundService> _logger;
    private readonly CoreDataAutomationWorkerState _state = new();

    public CoreDataAutomationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<CoreDataAutomationOptions> optionsMonitor,
        ILogger<CoreDataAutomationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Core data automation background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = _optionsMonitor.CurrentValue.GetIdleInterval();

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<CoreDataAutomationOrchestrator>();
                var cycleResult = await orchestrator.RunCycleAsync(_state, stoppingToken);
                delay = cycleResult.NextDelay;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Core data automation cycle failed.");
                delay = _optionsMonitor.CurrentValue.GetErrorRetryInterval();

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var syncErrorService = scope.ServiceProvider.GetRequiredService<SyncErrorService>();
                    await syncErrorService.RecordAsync(
                        "core_automation",
                        "cycle",
                        "background",
                        ex.Message,
                        cancellationToken: stoppingToken);
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Failed to persist core automation background error.");
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

        _logger.LogInformation("Core data automation background service stopped.");
    }
}
