namespace Backup.Application.Bulk.Models;

public sealed class BulkItem
{
    public required string UserName { get; set; }
    public string? UserId { get; set; }
    public BulkUserStatus UserStatus { get; set; }
    public int? Total { get; set; }
    public string? Cursor { get; set; }
    public int? Phase1Order { get; set; }
    public int? Phase2Order { get; set; }
}
