namespace Backup.App.Models.Media.Backup;

public class Chunk
{
    public int Id { get; set; }
    public List<ChunkData> Data { get; set; } = [];

    public Chunk Clone() => new() { Id = Id, Data = [.. Data.Select(data => data.Clone())] };
}

public class ChunkData
{
    public required string Path { get; set; }
    public long? FileSize { get; set; }
    public string? Hash { get; set; }
    public uint? Crc32 { get; set; }

    public ChunkData Clone() =>
        new()
        {
            Path = Path,
            FileSize = FileSize,
            Hash = Hash,
            Crc32 = Crc32,
        };
}
