namespace Backup.Application.Config.Models;

public sealed class ConfigApiEntry
{
    public required string Key { get; set; }
    public required string Id { get; set; }
    public required string Url { get; set; }
    public required Dictionary<string, object?> Variables { get; set; }
    public Dictionary<string, bool>? Features { get; set; }
    public Dictionary<string, bool>? FieldToggles { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
}
