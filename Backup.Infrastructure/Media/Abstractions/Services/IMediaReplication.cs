using Backup.Infrastructure.Media.Models;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaReplication
{
    public Task Replicate(
        List<Download> downloads,
        IEnumerable<IMediaStorage> data,
        IMediaStorage current,
        CancellationToken cancellationToken = default
    );
}
