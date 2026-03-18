namespace Backup.App.Models.Utils;

public class ZipEntry
{
    public required string FullName { get; set; }
    public required long FileSize { get; set; }
    public required uint Crc32 { get; set; }
    public required DateTimeOffset LastWriteTime { get; set; }
}
