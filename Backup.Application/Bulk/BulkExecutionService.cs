namespace Backup.Application.Bulk;

public sealed class BulkExecutionService : IBulkExecutionService
{
    public async Task Run(IBulkExecutionCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await command.RunImport(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await command.RunVerify(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await command.RunPhase1(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await command.RunPhase2(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await command.RunPhase2Reset(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await command.Prune(cancellationToken);
    }
}
