namespace Backup.App.Models.Bulk;

public class Bulk
{
    public required User User { get; set; }
    public int? Total { get; set; }
    public string? Cursor { get; set; }
    public Order Order { get; set; } = new();
}

public class Order
{
    public int? Phase1 { get; set; } = 0;
    public int? Phase2 { get; set; } = 0;
}
