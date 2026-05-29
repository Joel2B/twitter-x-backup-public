using Backup.Infrastructure.Models.Config.Downloads;

namespace Backup.Infrastructure.Models.Config.Data.Backup;

public class ChunkConfig : PathConfig
{
    public int Count { get; set; }
    public required PathChunkConfig Path { get; set; }
    public required PathConfig Data { get; set; }
    public required PathConfig Zip { get; set; }
}

public class PathChunkConfig : PathConfig
{
    public int Increase { get; set; }
    public int Size { get; set; }
}

