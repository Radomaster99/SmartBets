using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartBets.Data;
using SmartBets.Entities;

namespace SmartBets.Services;

public class TheOddsViewerRefreshStateService
{
    private const string ViewerHeartbeatSettingKey = "viewer_heartbeat_enabled";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<TheOddsApiOptions> _optionsMonitor;
    private readonly ILogger<TheOddsViewerRefreshStateService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CachedViewerRefreshState? _cache;
    private bool? _fallbackLiveOddsHeartbeatEnabled;
    private DateTime? _fallbackUpdatedAtUtc;
    private string? _fallbackUpdatedBy;

    public TheOddsViewerRefreshStateService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<TheOddsApiOptions> optionsMonitor,
        ILogger<TheOddsViewerRefreshStateService> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<TheOddsViewerRefreshStateSnapshot> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var cached = _cache;
        if (cached is not null && cached.ExpiresAtUtc > nowUtc)
            return cached.Snapshot;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            cached = _cache;
            nowUtc = DateTime.UtcNow;
            if (cached is not null && cached.ExpiresAtUtc > nowUtc)
                return cached.Snapshot;

            TheOddsViewerRefreshStateSnapshot snapshot;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var setting = await dbContext.TheOddsRuntimeSettings
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.SettingKey == ViewerHeartbeatSettingKey, cancellationToken);

                snapshot = BuildSnapshot(setting);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                snapshot = BuildSnapshot(
                    null,
                    _fallbackLiveOddsHeartbeatEnabled,
                    _fallbackUpdatedAtUtc,
                    _fallbackUpdatedBy);

                _logger.LogWarning(
                    ex,
                    "Falling back to in-memory The Odds viewer refresh state. The runtime settings table may not be available yet.");
            }

            _cache = new CachedViewerRefreshState(snapshot, nowUtc.Add(CacheTtl));
            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TheOddsViewerRefreshStateSnapshot> SetLiveOddsHeartbeatEnabledAsync(
        bool enabled,
        string? updatedBy,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var nowUtc = DateTime.UtcNow;
            var normalizedUpdatedBy = NormalizeUpdatedBy(updatedBy);
            TheOddsViewerRefreshStateSnapshot snapshot;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var setting = await dbContext.TheOddsRuntimeSettings
                    .SingleOrDefaultAsync(x => x.SettingKey == ViewerHeartbeatSettingKey, cancellationToken);

                if (setting is null)
                {
                    setting = new TheOddsRuntimeSetting
                    {
                        SettingKey = ViewerHeartbeatSettingKey
                    };

                    dbContext.TheOddsRuntimeSettings.Add(setting);
                }

                setting.BoolValue = enabled;
                setting.UpdatedAtUtc = nowUtc;
                setting.UpdatedBy = normalizedUpdatedBy;

                await dbContext.SaveChangesAsync(cancellationToken);
                snapshot = BuildSnapshot(setting);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _fallbackLiveOddsHeartbeatEnabled = enabled;
                _fallbackUpdatedAtUtc = nowUtc;
                _fallbackUpdatedBy = normalizedUpdatedBy;

                snapshot = BuildSnapshot(
                    null,
                    _fallbackLiveOddsHeartbeatEnabled,
                    _fallbackUpdatedAtUtc,
                    _fallbackUpdatedBy);

                _logger.LogWarning(
                    ex,
                    "Failed to persist The Odds viewer refresh state. Using in-memory fallback on this app instance.");
            }

            _cache = new CachedViewerRefreshState(snapshot, nowUtc.Add(CacheTtl));
            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    private TheOddsViewerRefreshStateSnapshot BuildSnapshot(
        TheOddsRuntimeSetting? setting,
        bool? liveOddsHeartbeatEnabledOverride = null,
        DateTime? updatedAtUtcOverride = null,
        string? updatedByOverride = null)
    {
        var options = _optionsMonitor.CurrentValue;
        var liveOddsHeartbeatEnabled = liveOddsHeartbeatEnabledOverride ?? setting?.BoolValue ?? true;
        var providerEnabled = options.Enabled;
        var providerConfigured = options.IsConfigured();
        var configViewerDrivenRefreshEnabled = options.EnableViewerDrivenRefresh;
        var effectiveViewerDrivenRefreshEnabled =
            providerEnabled &&
            providerConfigured &&
            configViewerDrivenRefreshEnabled &&
            liveOddsHeartbeatEnabled;

        return new TheOddsViewerRefreshStateSnapshot(
            liveOddsHeartbeatEnabled,
            providerEnabled,
            providerConfigured,
            configViewerDrivenRefreshEnabled,
            effectiveViewerDrivenRefreshEnabled,
            options.EnableReadDrivenCatchUp,
            (int)options.GetViewerHeartbeatTtl().TotalSeconds,
            (int)options.GetViewerRefreshInterval().TotalSeconds,
            updatedAtUtcOverride ?? setting?.UpdatedAtUtc,
            updatedByOverride ?? setting?.UpdatedBy);
    }

    private static string? NormalizeUpdatedBy(string? updatedBy)
    {
        if (string.IsNullOrWhiteSpace(updatedBy))
            return null;

        var normalized = updatedBy.Trim();
        return normalized.Length <= 200
            ? normalized
            : normalized[..200];
    }

    private sealed record CachedViewerRefreshState(
        TheOddsViewerRefreshStateSnapshot Snapshot,
        DateTime ExpiresAtUtc);
}

public sealed record TheOddsViewerRefreshStateSnapshot(
    bool LiveOddsHeartbeatEnabled,
    bool TheOddsProviderEnabled,
    bool TheOddsProviderConfigured,
    bool ConfigViewerDrivenRefreshEnabled,
    bool EffectiveViewerDrivenRefreshEnabled,
    bool ReadDrivenCatchUpEnabled,
    int ViewerHeartbeatTtlSeconds,
    int ViewerRefreshIntervalSeconds,
    DateTime? UpdatedAtUtc,
    string? UpdatedBy);
