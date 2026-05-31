namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupProgressDecision
{
    public required int Percent { get; init; }
    public required bool ShouldLog { get; init; }
}
