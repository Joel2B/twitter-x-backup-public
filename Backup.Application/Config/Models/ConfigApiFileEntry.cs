namespace Backup.Application.Config.Models;

public sealed class ConfigApiFileEntry
{
    public required string Key { get; init; }
    public string? Id { get; init; }
    public bool HasRequest { get; init; }
}
