using Backup.Infrastructure.Models.Media;
using Backup.Infrastructure.Models.Posts;

namespace Backup.Infrastructure.Interfaces.Services.Media;

public interface IMediaProcessing
{
    public Task Process(List<MediaInput> posts);
    public List<Download> GetMedia();
    public List<Download> GetFilteredMedia();
}
