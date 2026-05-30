using Backup.Application.Bulk.Models;
using Backup.Application.Bulk.Ports;

namespace Backup.Application.Bulk;

public interface IBulkImportService
{
    Task Run(
        IBulkImportCommand command,
        BulkImportOptions options,
        CancellationToken cancellationToken
    );
}
