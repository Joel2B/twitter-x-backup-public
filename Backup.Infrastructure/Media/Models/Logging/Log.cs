namespace Backup.Infrastructure.Models.Media.Logging;

public class Logs
{
    public required string Id { get; set; }
    public required List<Log> Messages { get; set; }
}

public class Log
{
    public required string Id { get; set; }
    public required string Message { get; set; }
}
