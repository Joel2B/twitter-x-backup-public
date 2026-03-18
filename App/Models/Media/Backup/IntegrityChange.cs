namespace Backup.App.Models.Media.Backup;

public class IntegrityChange
{
    public int Id { get; set; }
    public required string Path { get; set; }
    public FileSize? FileSize { get; set; }
    public Crc32? Crc32 { get; set; }
}

public class FileSize
{
    public long? Diff1 { get; set; }
    public long? Diff2 { get; set; }
}

public class Crc32
{
    public long? Diff1 { get; set; }
    public long? Diff2 { get; set; }
}
