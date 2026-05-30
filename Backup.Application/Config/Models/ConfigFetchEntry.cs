namespace Backup.Application.Config.Models;

public sealed class ConfigFetchEntry
{
    public required string Key { get; set; }
    public object? CountRaw { get; set; }
    public object? ApiRaw { get; set; }
    public int Count { get; set; }
    public int Api { get; set; }
}
