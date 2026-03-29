namespace SmartBets.Entities;

public class Bookmaker
{
    public long Id { get; set; }
    public long ApiBookmakerId { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<PreMatchOdd> PreMatchOdds { get; set; } = new List<PreMatchOdd>();
}