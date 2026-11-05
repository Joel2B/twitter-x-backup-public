namespace Backup.Application.Bulk.Models;

public sealed class BulkItem
{
    public required string UserName { get; set; }
    public string? UserId { get; set; }
    public BulkUserStatus UserStatus { get; set; }
    public int? Total { get; set; }
    public string? Cursor { get; set; }
    public string? VideoCursor { get; set; }
    public bool VideoCompleted { get; set; }
    public string? PhotoCursor { get; set; }
    public bool PhotoCompleted { get; set; }
    public string? Phase2VideoCursor { get; set; }
    public bool Phase2VideoCompleted { get; set; }
    public string? Phase2PhotoCursor { get; set; }
    public bool Phase2PhotoCompleted { get; set; }
    public int? Phase1Order { get; set; }
    public int? Phase2Order { get; set; }
}
