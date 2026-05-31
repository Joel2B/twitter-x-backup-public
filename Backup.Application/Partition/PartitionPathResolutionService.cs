using Backup.Application.IO;
using Backup.Application.Partition.Models;

namespace Backup.Application.Partition;

public sealed class PartitionPathResolutionService : IPartitionPathResolutionService
{
    public string Resolve(PartitionPathSource source)
    {
        IReadOnlyList<string> resolved = PathAliasResolutionPolicy.ResolveAliases(
            source.Paths,
            source.Aliases
        );
        return PathCompositionPolicy.ComposePath(resolved, source.BaseDirectory);
    }
}
