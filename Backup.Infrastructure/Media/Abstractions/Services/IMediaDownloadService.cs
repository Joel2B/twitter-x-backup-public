using Backup.Infrastructure.Media.Models;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaDownloadService
{
    public Task Download(List<Download> downloads, IMediaStorage data);
}
