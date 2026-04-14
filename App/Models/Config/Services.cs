namespace Backup.App.Models.Config;

public class Services
{
    public required Recovery Recovery { get; set; }
    public required DumpService Dump { get; set; }
}

public class Recovery
{
    public bool Enabled { get; set; }
}

public class DumpService
{
    public int Count { get; set; }
}
