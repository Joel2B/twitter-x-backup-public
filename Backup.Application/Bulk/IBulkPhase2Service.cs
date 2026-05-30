using Backup.Application.Bulk.Models;
using Backup.Application.Bulk.Ports;

namespace Backup.Application.Bulk;

public interface IBulkPhase2Service
{
    Task Run(
        IBulkPhase2Command command,
        BulkPhase2Options options,
        string origin,
        CancellationToken cancellationToken
    );
}
