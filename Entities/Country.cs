namespace SmartBets.Entities;

public class Country
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? FlagUrl { get; set; }

    public ICollection<League> Leagues { get; set; } = new List<League>();
    public ICollection<Team> Teams { get; set; } = new List<Team>();
}