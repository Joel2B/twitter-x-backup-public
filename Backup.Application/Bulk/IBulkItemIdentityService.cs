using Backup.Application.Bulk.Models;

namespace Backup.Application.Bulk;

public interface IBulkItemIdentityService
{
    string GetKey(BulkItem item);
}
