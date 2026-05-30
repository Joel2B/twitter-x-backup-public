using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Interfaces.Services.Media;

public interface IMediaDownloadService
{
    public Task Download(List<Download> downloads, IMediaStorage data);
}

