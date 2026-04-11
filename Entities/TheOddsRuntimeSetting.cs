namespace SmartBets.Entities;

public class TheOddsRuntimeSetting
{
    public long Id { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public bool? BoolValue { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
