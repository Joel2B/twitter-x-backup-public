using Backup.Application.Media.Prune;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;

namespace Backup.Infrastructure.Media.Services;

public class MediaPrune(
    IMediaPruneExecutionService mediaPruneExecutionService,
    IMediaDownloadModelMapper mediaDownloadModelMapper
) : IMediaPrune
{
    private readonly IMediaPruneExecutionService _mediaPruneExecutionService = mediaPruneExecutionService;
    private readonly IMediaDownloadModelMapper _mediaDownloadModelMapper = mediaDownloadModelMapper;

    public Task Prune(List<Download> downloads)
    {
        IReadOnlyList<Backup.Application.Media.Models.MediaDownload> appDownloads =
            _mediaDownloadModelMapper.ToApplication(downloads);
        IReadOnlyList<Backup.Application.Media.Models.MediaDownload> pruned =
            _mediaPruneExecutionService.Execute(appDownloads);

        downloads.Clear();
        downloads.AddRange(_mediaDownloadModelMapper.ToInfrastructure(pruned));

        return Task.CompletedTask;
    }
}
