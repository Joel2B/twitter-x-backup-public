namespace Backup.Application.Config.Models;

public sealed class ConfigApiProjection
{
    public required string Key { get; init; }
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required Dictionary<string, object?> Variables { get; init; }
    public Dictionary<string, bool>? Features { get; init; }
    public Dictionary<string, bool>? FieldToggles { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
}
