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
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CachedViewerRefreshState? _cache;

    public TheOddsViewerRefreshStateService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<TheOddsApiOptions> optionsMonitor)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
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

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var setting = await dbContext.TheOddsRuntimeSettings
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.SettingKey == ViewerHeartbeatSettingKey, cancellationToken);

            var snapshot = BuildSnapshot(setting);
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
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var nowUtc = DateTime.UtcNow;

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
            setting.UpdatedBy = NormalizeUpdatedBy(updatedBy);

            await dbContext.SaveChangesAsync(cancellationToken);

            var snapshot = BuildSnapshot(setting);
            _cache = new CachedViewerRefreshState(snapshot, nowUtc.Add(CacheTtl));
            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    private TheOddsViewerRefreshStateSnapshot BuildSnapshot(TheOddsRuntimeSetting? setting)
    {
        var options = _optionsMonitor.CurrentValue;
        var liveOddsHeartbeatEnabled = setting?.BoolValue ?? true;
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
            setting?.UpdatedAtUtc,
            setting?.UpdatedBy);
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
