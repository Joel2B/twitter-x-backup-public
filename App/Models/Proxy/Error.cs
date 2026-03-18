namespace Backup.App.Models.Proxy;

public class Error
{
    public required ErrorMessage Message { get; set; }
    public int TotalDuplicates { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
}

public class ErrorMessage
{
    public required string Short { get; set; }
    public required string Extended { get; set; }
}
