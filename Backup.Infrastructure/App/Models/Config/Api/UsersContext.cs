namespace Backup.Infrastructure.Models.Config.Api;

public sealed class UsersContext
{
    public required string UserId { get; init; }
    public required Dictionary<string, ApiConfig> Api { get; init; }
}

