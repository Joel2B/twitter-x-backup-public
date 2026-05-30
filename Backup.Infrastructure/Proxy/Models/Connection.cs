namespace Backup.Infrastructure.Proxy.Models;

public class Connection
{
    public int TotalUses { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
}
