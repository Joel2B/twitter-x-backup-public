namespace Backup.Application.Proxy.Models;

public sealed class ProxyRuntimeError
{
    public required string Short { get; set; }
    public required string Extended { get; set; }
    public int TotalDuplicates { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
}
