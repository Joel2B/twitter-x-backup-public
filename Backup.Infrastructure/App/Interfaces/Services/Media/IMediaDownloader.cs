using Backup.App.Models.Media;

namespace Backup.App.Interfaces.Services.Media;

public interface IMediaDownloader
{
    public Task<Stream> Download(DataDownload data, IMediaData mediaData, CancellationToken token);
}
