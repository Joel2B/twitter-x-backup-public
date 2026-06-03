using Backup.Application.Bulk.Models;
using Backup.Application.Bulk.Ports;

namespace Backup.Application.Bulk;

public interface IBulkVerifyService
{
    Task<IReadOnlyList<BulkVerifyRow>> Run(
        IBulkVerifyCommand command,
        CancellationToken cancellationToken = default
    );
}
