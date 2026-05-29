using Backup.App.Models.Config.ApiRequest;

namespace Backup.App.Models.Config.Api;

public class ApiConfig
{
    public required string Id { get; set; }
    public bool Enabled { get; set; } = true;
    public required Request Request { get; set; }
}
