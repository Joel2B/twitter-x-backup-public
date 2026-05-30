using ApiRequest = Backup.Infrastructure.Models.Config.Request.Request;

namespace Backup.Infrastructure.Models.Config.Api;

public class ApiConfig
{
    public required string Id { get; set; }
    public bool Enabled { get; set; } = true;
    public required ApiRequest Request { get; set; }
}
