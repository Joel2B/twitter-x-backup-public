namespace Backup.Application.Bulk;

public interface IBulkExecutionCommand
{
    Task RunImport(CancellationToken cancellationToken);
    Task RunVerify();
    Task RunPhase1(CancellationToken cancellationToken);
    Task RunPhase2(CancellationToken cancellationToken);
    Task RunPhase2Reset();
    Task Prune();
}
