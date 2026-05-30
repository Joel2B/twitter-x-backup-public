using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Interfaces.Services.Media;

public interface IMediaDownloader
{
    public Task<Stream> Download(
        DataDownload data,
        IMediaStorage mediaData,
        CancellationToken token
    );
}

