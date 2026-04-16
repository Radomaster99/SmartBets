namespace SmartBets.Dtos;

public class AdminLoginRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AdminSessionDto
{
    public bool Configured { get; set; }
    public bool Authenticated { get; set; }
    public string? AuthenticationType { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public string? AuthSource { get; set; }
    public DateTime? SessionExpiresAtUtc { get; set; }
}
