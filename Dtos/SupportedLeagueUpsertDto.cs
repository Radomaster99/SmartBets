namespace SmartBets.Dtos;

public class SupportedLeagueUpsertDto
{
    public long LeagueApiId { get; set; }
    public int Season { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
}

public class SupportedLeaguesBulkUpsertRequestDto
{
    public List<SupportedLeagueUpsertDto> Items { get; set; } = new();
}

public class SupportedLeaguesBulkUpsertResultDto
{
    public int Received { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int Failed { get; set; }
    public List<SupportedLeaguesBulkUpsertItemResultDto> Results { get; set; } = new();
}

public class SupportedLeaguesBulkUpsertItemResultDto
{
    public long? Id { get; set; }
    public long LeagueApiId { get; set; }
    public int Season { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public class SupportedLeagueUpdateDto
{
    public bool? IsActive { get; set; }
    public int? Priority { get; set; }
}
