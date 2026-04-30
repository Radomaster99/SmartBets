namespace SmartBets.Services;

public class CoreAutomationSingleJobResult
{
    public int RequestsUsed { get; set; }
    public int ProcessedItems { get; set; }
    public string? Action { get; set; }
    public bool Failed { get; set; }
}

public class CoreAutomationTargetJobResult
{
    public int RequestsUsed { get; set; }
    public int ProcessedItems { get; set; }
    public List<string> SyncedKeys { get; } = new();
}
