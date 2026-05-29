using Backup.App.Models.Media;

namespace Backup.App.Interfaces.Services.Media;

public interface IMediaReplication
{
    public Task Replicate(
        List<Download> downloads,
        IEnumerable<IMediaData> data,
        IMediaData current
    );
}
