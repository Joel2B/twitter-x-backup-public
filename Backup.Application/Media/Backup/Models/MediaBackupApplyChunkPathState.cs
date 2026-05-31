namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupApplyChunkPathState
{
    public required string SourcePath { get; init; }
    public required bool HasHash { get; init; }
}
