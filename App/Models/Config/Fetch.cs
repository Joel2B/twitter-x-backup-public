namespace Backup.App.Models.Config;

public class Fetch
{
    public required Source Current { get; set; }
    public required List<Source> Sources { get; set; }
}
