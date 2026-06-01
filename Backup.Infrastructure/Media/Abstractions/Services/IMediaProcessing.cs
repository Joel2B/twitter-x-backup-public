using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Posts.Models;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaProcessing
{
    public Task Process(List<MediaInput> posts, CancellationToken cancellationToken = default);
    public List<Download> GetMedia();
    public List<Download> GetFilteredMedia();
}
