namespace Backup.Application.Partition;

public interface IPartitionPathProbeService
{
    string? Probe(string path);
}
