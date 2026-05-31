namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupDirectPathScanResult
{
    public required bool ShouldThrowMissingSource { get; init; }
    public required bool ShouldIncludeDirectPath { get; init; }
    public required string IncludedPath { get; init; }
}
