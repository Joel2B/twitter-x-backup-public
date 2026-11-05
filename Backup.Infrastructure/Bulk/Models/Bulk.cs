namespace Backup.Infrastructure.Bulk.Models;

public class BulkData
{
    public required User User { get; set; }
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
    public Order Order { get; set; } = new();
}

public class Order
{
    public int? Phase1 { get; set; } = 0;
    public int? Phase2 { get; set; } = 0;
}
