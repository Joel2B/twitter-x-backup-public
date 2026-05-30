namespace Backup.Application.BackupRun.Models;

public sealed class BackupRunSourceExecution
{
    public required string UserId { get; init; }
    public required string SourceId { get; init; }
    public required string ApiId { get; init; }
    public required int Count { get; init; }
    public required BackupRunRequestExecution Request { get; init; }
}
