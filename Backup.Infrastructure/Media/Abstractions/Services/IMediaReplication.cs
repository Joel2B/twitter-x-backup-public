using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Interfaces.Services.Media;

public interface IMediaReplication
{
    public Task Replicate(
        List<Download> downloads,
        IEnumerable<IMediaStorage> data,
        IMediaStorage current
    );
}

