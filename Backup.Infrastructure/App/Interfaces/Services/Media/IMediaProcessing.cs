using Backup.App.Models.Media;
using Backup.App.Models.Posts;

namespace Backup.App.Interfaces.Services.Media;

public interface IMediaProcessing
{
    public Task Process(List<MediaInput> posts);
    public List<Download> GetMedia();
    public List<Download> GetFilteredMedia();
}
