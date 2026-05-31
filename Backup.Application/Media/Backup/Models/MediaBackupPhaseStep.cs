namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupPhaseStep
{
    public required string StepId { get; init; }
    public required int Order { get; init; }
    public required string TimerName { get; init; }
    public required bool SkipWhenStopped { get; init; }
}
