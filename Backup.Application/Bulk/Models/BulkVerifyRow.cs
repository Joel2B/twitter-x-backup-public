namespace Backup.Application.Bulk.Models;

public sealed class BulkVerifyRow
{
    public string? UserId { get; set; }
    public required string UserName { get; set; }
    public int? TotalBulk { get; set; }
    public int TotalPost { get; set; }
}
