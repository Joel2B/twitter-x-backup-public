namespace Backup.Application.Bulk.Models;

public sealed class BulkSourceItem
{
    public required string UserName { get; set; }
    public BulkSourceType Type { get; set; }
}
