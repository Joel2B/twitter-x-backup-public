using Backup.Application.Media.Filter;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Models.Logging;

namespace Backup.Infrastructure.Media.Services;

public class MediaFilter(
    IMediaLogger _mediaLogger,
    IMediaErrorExclusionService mediaErrorExclusionService,
    IMediaErrorDownloadFilterService mediaErrorDownloadFilterService,
    IMediaDownloadModelMapper mediaDownloadModelMapper
) : IMediaFilter
{
    private readonly IMediaLogger _mediaLogger = _mediaLogger;
    private readonly IMediaErrorExclusionService _mediaErrorExclusionService =
        mediaErrorExclusionService;
    private readonly IMediaErrorDownloadFilterService _mediaErrorDownloadFilterService =
        mediaErrorDownloadFilterService;
    private readonly IMediaDownloadModelMapper _mediaDownloadModelMapper = mediaDownloadModelMapper;

    public async Task Check(
        List<Download> downloads,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<Logs> logs = await _mediaLogger.GetErrors() ?? [];

        IReadOnlySet<string> ids = _mediaErrorExclusionService.GetExcludedIds(
            logs.SelectMany(log => log.Messages)
                .Select(message => new MediaErrorMessage
                {
                    Id = message.Id,
                    Message = message.Message,
                })
        );

        IReadOnlyList<Backup.Application.Media.Models.MediaDownload> appDownloads =
            _mediaDownloadModelMapper.ToApplication(downloads);
        IReadOnlyList<Backup.Application.Media.Models.MediaDownload> filtered =
            _mediaErrorDownloadFilterService.FilterByExcludedUrls(appDownloads, ids);

        downloads.Clear();
        downloads.AddRange(_mediaDownloadModelMapper.ToInfrastructure(filtered));
    }
}
