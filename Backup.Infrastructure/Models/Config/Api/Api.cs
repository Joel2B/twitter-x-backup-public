using Backup.Infrastructure.Models.Config.ApiRequest;

namespace Backup.Infrastructure.Models.Config.Api;

public class ApiConfig
{
    public required string Id { get; set; }
    public bool Enabled { get; set; } = true;
    public required Request Request { get; set; }
}

