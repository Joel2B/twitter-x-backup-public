namespace Backup.App.Models.Config.Api;

public sealed class ApiContext
{
    public required string Id { get; init; }
    public required Request.Request Request { get; init; }
    public required int Count { get; set; }
    public required string UserId { get; init; }
}
