namespace SmartBets.Models.TheOddsApi;

public class TheOddsApiSport
{
    public string Key { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool HasOutrights { get; set; }
}
