namespace Backup.Infrastructure.Models.Media.Backup;

public class BackupChunks
{
    public DateTime Date { get; set; } = DateTime.Now;
    public required Chunks Chunks { get; set; }
}

public class Chunks
{
    public required int Total { get; set; }
    public required PathChunks Path { get; set; }
    public List<int> Ids { get; set; } = [];
}

public class PathChunks
{
    public int Increase { get; set; }
}

