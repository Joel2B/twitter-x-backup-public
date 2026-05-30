namespace Backup.Infrastructure.Models.Proxy;

public class ProxyData
{
    public required ProxyDataConfig Proxy { get; set; }
    public List<Connection> Connections { get; set; } = [];
    public List<Error> Errors { get; set; } = [];
    public DateTime Date { get; set; } = DateTime.Now;
    public Status Status { get; set; } = new();
}
