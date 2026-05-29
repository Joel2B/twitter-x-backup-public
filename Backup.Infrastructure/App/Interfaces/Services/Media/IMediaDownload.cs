using Backup.App.Models.Media;

namespace Backup.App.Interfaces.Services.Media;

public interface IMediaDownload
{
    public Task Download(List<Download> downloads, IMediaData data);
}
