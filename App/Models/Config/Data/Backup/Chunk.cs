namespace Backup.App.Models.Config.Data.Backup;

public class Chunk : Downloads.Path
{
    public int Count { get; set; }
    public required Path Path { get; set; }
    public required Downloads.Path Data { get; set; }
    public required Downloads.Path Zip { get; set; }
}

public class Path : Downloads.Path
{
    public int Increase { get; set; }
    public int Size { get; set; }
}
