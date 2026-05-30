namespace Backup.Infrastructure.Proxy.Models;

public enum StatusEnum
{
    Inactive,
    Active,
}

public class Status
{
    public StatusEnum Current { get; set; } = StatusEnum.Active;
    public DateTime? Date { get; set; }
}
