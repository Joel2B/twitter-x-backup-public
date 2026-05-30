using ApiRequest = Backup.Infrastructure.Models.Config.Request.Request;

namespace Backup.Infrastructure.Models.Config.Api;

public sealed class ApiContext
{
    public required string Id { get; init; }
    public required ApiRequest Request { get; init; }
    public required int Count { get; set; }
    public required string UserId { get; init; }
}
