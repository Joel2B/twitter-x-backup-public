namespace Backup.Application.Bulk;

public interface IBulkExecutionCommand
{
    Task RunImport(CancellationToken cancellationToken);
    Task RunVerify(CancellationToken cancellationToken);
    Task RunPhase1(CancellationToken cancellationToken);
    Task RunPhase2(CancellationToken cancellationToken);
    Task RunPhase2Reset(CancellationToken cancellationToken);
    Task Prune(CancellationToken cancellationToken);
}
