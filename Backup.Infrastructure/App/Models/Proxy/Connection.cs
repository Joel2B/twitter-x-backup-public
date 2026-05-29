namespace Backup.App.Models.Proxy;

public class Connection
{
    public int TotalUses { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
}
