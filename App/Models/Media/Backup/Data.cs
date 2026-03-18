namespace Backup.App.Models.Media.Backup;

public class Backup
{
    public DateTime Date { get; set; } = DateTime.Now;
    public required Chunks Chunks { get; set; }
}

public class Chunks
{
    public required int Total { get; set; }
    public required Path Path { get; set; }
    public List<int> Ids { get; set; } = [];
}

public class Path
{
    public int Increase { get; set; }
}
