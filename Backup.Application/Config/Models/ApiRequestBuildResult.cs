namespace Backup.Application.Config.Models;

public sealed class ApiRequestBuildResult
{
    public required string Url { get; init; }
    public required Dictionary<string, object?> Variables { get; init; }
    public required Dictionary<string, bool> Features { get; init; }
    public required Dictionary<string, bool> FieldToggles { get; init; }
    public required Dictionary<string, string> Headers { get; init; }
}
