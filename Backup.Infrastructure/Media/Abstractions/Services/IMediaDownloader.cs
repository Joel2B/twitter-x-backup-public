using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaDownloader
{
    public Task<Stream> Download(
        DataDownload data,
        IMediaStorage mediaData,
        CancellationToken token
    );
}
