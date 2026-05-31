namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupPhaseExecutionStep
{
    public required string StepId { get; init; }
    public required string TimerName { get; init; }
}
