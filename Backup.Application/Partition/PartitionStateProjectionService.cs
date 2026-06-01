using Backup.Application.Partition.Models;

namespace Backup.Application.Partition;

public sealed class PartitionStateProjectionService : IPartitionStateProjectionService
{
    public PartitionState ToState(PartitionStateSource source) =>
        new()
        {
            Id = source.Id,
            Type = source.Type,
            Tags = source.Tags,
            Size = source.Size,
            UsableSpace = source.UsableSpace,
            Enabled = source.Enabled,
            CurrentSize = source.CurrentSize,
        };

    public IReadOnlyList<PartitionState> ToStates(IEnumerable<PartitionStateSource> sources) =>
        sources.Select(ToState).ToList();
}
