namespace SmartBets.Services;

public class DataRetentionOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalHours { get; set; } = 12;
    public int ErrorRetryMinutes { get; set; } = 30;
    public int SyncErrorsRetentionDays { get; set; } = 30;
    public int LiveOddsRetentionDays { get; set; } = 3;
    public int PreMatchOddsRetentionDays { get; set; } = 30;

    public TimeSpan GetInterval() => TimeSpan.FromHours(Math.Clamp(IntervalHours, 1, 48));
    public TimeSpan GetErrorRetryInterval() => TimeSpan.FromMinutes(Math.Clamp(ErrorRetryMinutes, 5, 240));
    public int GetSyncErrorsRetentionDays() => Math.Clamp(SyncErrorsRetentionDays, 1, 365);
    public int GetLiveOddsRetentionDays() => Math.Clamp(LiveOddsRetentionDays, 1, 30);
    public int GetPreMatchOddsRetentionDays() => Math.Clamp(PreMatchOddsRetentionDays, 3, 365);
}
