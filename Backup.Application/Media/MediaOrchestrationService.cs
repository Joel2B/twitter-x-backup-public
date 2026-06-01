using Backup.Application.Media.Models;
using Backup.Application.Media.Ports;

namespace Backup.Application.Media;

public sealed class MediaOrchestrationService : IMediaOrchestrationService
{
    public async Task Run(
        IMediaOrchestrationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<global::Backup.Domain.Posts.MediaInput> posts = await command.GetMediaInputs(
            cancellationToken
        );

        if (posts.Count == 0)
            return;

        cancellationToken.ThrowIfCancellationRequested();
        MediaProcessingResult result = await command.Process(posts, cancellationToken);

        List<MediaDownload> all = result.All.Select(download => download.Clone()).ToList();
        List<MediaDownload> filtered = result
            .Filtered.Select(download => download.Clone())
            .ToList();

        cancellationToken.ThrowIfCancellationRequested();
        await command.Prune(all, cancellationToken);
        await command.Filter(filtered, cancellationToken);

        foreach (string storageId in command.GetStorageIds())
        {
            if (!command.HasMaintenance(storageId))
                continue;

            List<MediaDownload> filteredCloned = filtered
                .Select(download => download.Clone())
                .ToList();
            List<MediaDownload> filteredIntegrity = filtered
                .Select(download => download.Clone())
                .ToList();

            cancellationToken.ThrowIfCancellationRequested();
            await command.PruneStorage(storageId, all, cancellationToken);
            await command.CheckStorageData(storageId, filteredCloned, cancellationToken);
            await command.CheckStorageIntegrity(storageId, filteredIntegrity, cancellationToken);

            filteredCloned.AddRange(filteredIntegrity);

            cancellationToken.ThrowIfCancellationRequested();
            await command.DownloadToStorage(storageId, filteredCloned, cancellationToken);
            await command.Filter(filteredCloned, cancellationToken);
            await command.CheckStorageData(storageId, filteredCloned, cancellationToken);
            await command.ReplicateFromStorage(storageId, filteredCloned, cancellationToken);
            await command.Filter(filtered, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await command.RunBackups(filtered, cancellationToken);
    }
}
