using Backup.Application.Media.Integrity;
using Backup.Application.Media.Models;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;

namespace Backup.Infrastructure.Media.Services;

public class MediaIntegrity(IMediaIntegrityPolicyService integrityPolicyService) : IMediaIntegrity
{
    private readonly IMediaIntegrityPolicyService _integrityPolicyService = integrityPolicyService;

    public async Task Check(List<Download> downloads, IMediaDataMaintenance data)
    {
        List<MediaDownload> appDownloads = downloads
            .Select(download => new MediaDownload
            {
                Id = download.Id,
                Data = download
                    .Data.Select(item => new MediaDownloadData { Url = item.Url, Path = item.Path })
                    .ToList(),
            })
            .ToList();

        _integrityPolicyService.KeepSupported(appDownloads);

        downloads.Clear();
        downloads.AddRange(
            appDownloads.Select(download => new Download
            {
                Id = download.Id,
                Data = download
                    .Data.Select(item => new DataDownload { Url = item.Url, Path = item.Path })
                    .ToList(),
            })
        );

        await data.CheckIntegrity(downloads);
    }
}
