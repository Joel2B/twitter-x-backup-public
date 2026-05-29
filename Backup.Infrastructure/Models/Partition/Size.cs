using Backup.Infrastructure.Models.Config.Data;

namespace Backup.Infrastructure.Models.Partition;

public class PartitionSize
{
    private long _size;
    public long Size
    {
        get => _size;
        set => Interlocked.Exchange(ref _size, value);
    }
    public required PartitionConfig Partition { get; set; }

    public void Add(long size)
    {
        Interlocked.Add(ref _size, size);
    }
}

