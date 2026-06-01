using Backup.Application.Media.Integrity;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;

namespace Backup.Infrastructure.Media.Services;

public class MediaIntegrity(
    IMediaIntegrityPolicyService integrityPolicyService,
    IMediaDownloadModelMapper mediaDownloadModelMapper
) : IMediaIntegrity
{
    private readonly IMediaIntegrityPolicyService _integrityPolicyService = integrityPolicyService;
    private readonly IMediaDownloadModelMapper _mediaDownloadModelMapper = mediaDownloadModelMapper;

    public async Task Check(List<Download> downloads, IMediaDataMaintenance data)
    {
        List<Backup.Application.Media.Models.MediaDownload> appDownloads =
            _mediaDownloadModelMapper.ToApplication(downloads);

        _integrityPolicyService.KeepSupported(appDownloads);

        downloads.Clear();
        downloads.AddRange(_mediaDownloadModelMapper.ToInfrastructure(appDownloads));

        await data.CheckIntegrity(downloads);
    }
}
