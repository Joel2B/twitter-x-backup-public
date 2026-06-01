using Backup.Application.Media.Models;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;

namespace Backup.Infrastructure.Media.Services;

public sealed class MediaDownloadModelMapper : IMediaDownloadModelMapper
{
    public List<MediaDownload> ToApplication(IEnumerable<Download> downloads) =>
        downloads
            .Select(download => new MediaDownload
            {
                Id = download.Id,
                Data = download
                    .Data.Select(item => new MediaDownloadData { Url = item.Url, Path = item.Path })
                    .ToList(),
            })
            .ToList();

    public List<Download> ToInfrastructure(IEnumerable<MediaDownload> downloads) =>
        downloads
            .Select(download => new Download
            {
                Id = download.Id,
                Data = download
                    .Data.Select(item => new DataDownload { Url = item.Url, Path = item.Path })
                    .ToList(),
            })
            .ToList();
}
