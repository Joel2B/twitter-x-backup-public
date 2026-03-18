namespace Backup.App.Models.Proxy;

public class Data
{
    public required Proxy Proxy { get; set; }
    public List<Connection> Connections { get; set; } = [];
    public List<Error> Errors { get; set; } = [];
    public DateTime Date { get; set; } = DateTime.Now;
    public Status Status { get; set; } = new();
}
