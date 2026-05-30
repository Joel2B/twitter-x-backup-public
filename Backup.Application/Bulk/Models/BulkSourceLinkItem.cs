namespace Backup.Application.Bulk.Models;

public sealed class BulkSourceLinkItem
{
    public required string Link { get; set; }
    public required string UserName { get; set; }
    public BulkSourceType Type { get; set; } = BulkSourceType.None;
}
