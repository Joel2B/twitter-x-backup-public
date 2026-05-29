using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Interfaces.Services.Media;

public interface IMediaDownload
{
    public Task Download(List<Download> downloads, IMediaData data);
}

