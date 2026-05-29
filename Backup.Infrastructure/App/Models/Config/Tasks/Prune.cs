namespace Backup.App.Models.Config.Tasks;

public class Prune
{
    public required Data Data { get; set; }
}

public class PruneConfig
{
    public int KeepDays { get; set; }
    public int KeepCount { get; set; }
}

public class Data
{
    public required PruneConfig Post { get; set; }
}
