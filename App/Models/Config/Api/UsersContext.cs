namespace Backup.App.Models.Config.Api;

public sealed class UsersContext
{
    public required string UserId { get; init; }
    public required Dictionary<string, Api> Api { get; init; }
}
