namespace Backup.Infrastructure.Models.Proxy;

public class Connection
{
    public int TotalUses { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
}
