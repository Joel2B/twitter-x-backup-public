namespace Backup.Application.Bulk;

public interface IBulkExecutionService
{
    Task Run(IBulkExecutionCommand command, CancellationToken cancellationToken);
}
