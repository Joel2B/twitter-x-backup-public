namespace Backup.Application.Proxy.Models;

public sealed class ProxyRuntimeConnection
{
    public DateTime Date { get; set; } = DateTime.Now;
    public int TotalUses { get; set; }
}
