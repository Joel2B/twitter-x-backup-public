using Backup.Application.Media.Models;
using Backup.Application.Media.Ports;

namespace Backup.Application.Media;

public sealed class MediaOrchestrationService : IMediaOrchestrationService
{
    public async Task Run(IMediaOrchestrationCommand command)
    {
        IReadOnlyList<global::Backup.Domain.Posts.MediaInput> posts =
            await command.GetMediaInputs();

        if (posts.Count == 0)
            return;

        MediaProcessingResult result = await command.Process(posts);

        List<MediaDownload> all = result.All.Select(download => download.Clone()).ToList();
        List<MediaDownload> filtered = result
            .Filtered.Select(download => download.Clone())
            .ToList();

        await command.Prune(all);
        await command.Filter(filtered);

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

            await command.PruneStorage(storageId, all);
            await command.CheckStorageData(storageId, filteredCloned);
            await command.CheckStorageIntegrity(storageId, filteredIntegrity);

            filteredCloned.AddRange(filteredIntegrity);

            await command.DownloadToStorage(storageId, filteredCloned);
            await command.Filter(filteredCloned);
            await command.CheckStorageData(storageId, filteredCloned);
            await command.ReplicateFromStorage(storageId, filteredCloned);
            await command.Filter(filtered);
        }

        await command.RunBackups(filtered);
    }
}
