namespace StellarAnvil.Application.DTOs;

public class McpConfigurationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public Dictionary<string, string> Settings { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateMcpConfigurationDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public Dictionary<string, string> Settings { get; set; } = new();
}

public class UpdateMcpConfigurationDto
{
    public string? Name { get; set; }
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public Dictionary<string, string>? Settings { get; set; }
    public bool? IsActive { get; set; }
}

public class McpConnectionTestResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object>? Details { get; set; }
}