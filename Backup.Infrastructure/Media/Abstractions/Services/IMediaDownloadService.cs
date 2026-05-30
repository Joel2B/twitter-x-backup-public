using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaDownloadService
{
    public Task Download(List<Download> downloads, IMediaStorage data);
}
