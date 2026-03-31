namespace SmartBets.Services;

public class ApiFootballClientOptions
{
    public int MinRequestSpacingMs { get; set; } = 250;
    public int LowMinuteRemainingThreshold { get; set; } = 5;
    public int CriticalMinuteRemainingThreshold { get; set; } = 2;
    public int LowDailyRemainingThreshold { get; set; } = 500;
    public int CriticalDailyRemainingThreshold { get; set; } = 150;
    public int LowQuotaDelayMs { get; set; } = 500;
    public int CriticalQuotaDelayMs { get; set; } = 1500;

    public int GetMinRequestSpacingMs() => Math.Clamp(MinRequestSpacingMs, 0, 5000);
    public int GetLowMinuteRemainingThreshold() => Math.Clamp(LowMinuteRemainingThreshold, 1, 100);
    public int GetCriticalMinuteRemainingThreshold() => Math.Clamp(CriticalMinuteRemainingThreshold, 1, 50);
    public int GetLowDailyRemainingThreshold() => Math.Clamp(LowDailyRemainingThreshold, 1, 5000);
    public int GetCriticalDailyRemainingThreshold() => Math.Clamp(CriticalDailyRemainingThreshold, 1, 2000);
    public int GetLowQuotaDelayMs() => Math.Clamp(LowQuotaDelayMs, 0, 10000);
    public int GetCriticalQuotaDelayMs() => Math.Clamp(CriticalQuotaDelayMs, 0, 15000);
}
