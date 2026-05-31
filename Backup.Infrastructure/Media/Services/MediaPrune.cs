using Backup.Application.Media.Prune;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;

namespace Backup.Infrastructure.Media.Services;

public class MediaPrune(IMediaPruneSelectionService pruneSelectionService) : IMediaPrune
{
    private readonly IMediaPruneSelectionService _pruneSelectionService = pruneSelectionService;

    public Task Prune(List<Download> downloads)
    {
        foreach (Download download in downloads)
        {
            download.Data.RemoveAll(data => _pruneSelectionService.ShouldRemove(data.Url, data.Path));
        }

        downloads.RemoveAll(dl => dl.Data.Count == 0);

        return Task.CompletedTask;
    }
}
