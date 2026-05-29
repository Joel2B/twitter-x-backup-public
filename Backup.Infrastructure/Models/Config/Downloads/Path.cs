namespace Backup.Infrastructure.Models.Config.Downloads;

public class PathConfig
{
    public required List<string> Paths { get; set; }
    public string? File { get; set; }
}

