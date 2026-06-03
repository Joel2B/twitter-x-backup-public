using Backup.Application.Bulk.Models;
using Backup.Application.Bulk.Ports;

namespace Backup.Application.Bulk;

public interface IBulkPhase1Service
{
    Task Run(
        IBulkPhase1Command command,
        BulkPhase1Options options,
        string origin,
        CancellationToken cancellationToken
    );
}
