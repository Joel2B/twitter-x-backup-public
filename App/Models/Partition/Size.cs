namespace Backup.App.Models.Partition;

public class PartitionSize
{
    private long _size;
    public long Size
    {
        get => _size;
        set => Interlocked.Exchange(ref _size, value);
    }
    public required Models.Config.Data.Partition Partition { get; set; }

    public void Add(long size)
    {
        Interlocked.Add(ref _size, size);
    }
}
