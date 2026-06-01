using Backup.Application.Media.Models;

namespace Backup.Application.Media.Prune;

public sealed class MediaPruneExecutionService(IMediaPruneSelectionService mediaPruneSelectionService)
    : IMediaPruneExecutionService
{
    private readonly IMediaPruneSelectionService _mediaPruneSelectionService = mediaPruneSelectionService;

    public IReadOnlyList<MediaDownload> Execute(IReadOnlyList<MediaDownload> downloads) =>
        downloads
            .Select(download => new MediaDownload
            {
                Id = download.Id,
                Data = download
                    .Data.Where(data =>
                        !_mediaPruneSelectionService.ShouldRemove(data.Url, data.Path)
                    )
                    .Select(data => new MediaDownloadData { Url = data.Url, Path = data.Path })
                    .ToList(),
            })
            .Where(download => download.Data.Count > 0)
            .ToList();
}
