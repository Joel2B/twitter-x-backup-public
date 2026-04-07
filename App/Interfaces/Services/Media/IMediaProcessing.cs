using Backup.App.Models.Media;

namespace Backup.App.Interfaces.Services.Media;

public interface IMediaProcessing
{
    public Task Process(List<Models.Post.MediaInput> posts);
    public List<Download> GetMedia();
    public List<Download> GetFilteredMedia();
}
