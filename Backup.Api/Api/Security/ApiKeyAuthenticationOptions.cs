namespace Backup.Api.Security;

public sealed class ApiKeyAuthenticationOptions
{
    public const string ConfigurationSection = "Backup:Api:Auth";

    public bool Enabled { get; init; } = false;
    public string HeaderName { get; init; } = "X-Api-Key";
    public string? ApiKey { get; init; }
}
