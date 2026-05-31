using Backup.Application.Bulk.Models;

namespace Backup.Application.Bulk;

public sealed class BulkItemIdentityService : IBulkItemIdentityService
{
    public string GetKey(BulkItem item)
    {
        string userId = item.UserId?.Trim() ?? string.Empty;

        if (userId.Length > 0)
            return $"id:{userId}";

        string userName = (item.UserName ?? string.Empty).Trim().ToLowerInvariant();
        return $"name:{userName}";
    }
}
