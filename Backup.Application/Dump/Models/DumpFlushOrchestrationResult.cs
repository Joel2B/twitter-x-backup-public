namespace Backup.Application.Dump.Models;

public sealed class DumpFlushOrchestrationResult
{
    public required DumpFlushExecutionResult FlushResult { get; init; }

    public required DumpSessionCloseResolution SessionCloseResolution { get; init; }
}
