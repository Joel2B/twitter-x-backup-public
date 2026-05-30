using Backup.Application.Bulk.Ports;

namespace Backup.Application.Bulk;

public interface IBulkPhase2ResetService
{
    Task Run(IBulkPhase2ResetCommand command);
}
