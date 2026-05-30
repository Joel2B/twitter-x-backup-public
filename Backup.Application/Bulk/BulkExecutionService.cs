namespace Backup.Application.Bulk;

public sealed class BulkExecutionService : IBulkExecutionService
{
    public async Task Run(IBulkExecutionCommand command, CancellationToken cancellationToken)
    {
        await command.RunImport(cancellationToken);
        await command.RunVerify();
        await command.RunPhase1(cancellationToken);
        await command.RunPhase2(cancellationToken);
        await command.RunPhase2Reset();
        await command.Prune();
    }
}
