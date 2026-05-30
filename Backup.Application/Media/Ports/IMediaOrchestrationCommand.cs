using Backup.Application.Media.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Media.Ports;

public interface IMediaOrchestrationCommand
{
    Task<IReadOnlyList<MediaInput>> GetMediaInputs();
    Task<MediaProcessingResult> Process(IReadOnlyList<MediaInput> posts);
    Task Prune(List<MediaDownload> downloads);
    Task Filter(List<MediaDownload> downloads);

    IReadOnlyList<string> GetStorageIds();
    bool HasMaintenance(string storageId);
    Task PruneStorage(string storageId, List<MediaDownload> downloads);
    Task CheckStorageData(string storageId, List<MediaDownload> downloads);
    Task CheckStorageIntegrity(string storageId, List<MediaDownload> downloads);
    Task DownloadToStorage(string storageId, List<MediaDownload> downloads);
    Task ReplicateFromStorage(string storageId, List<MediaDownload> downloads);

    Task RunBackups(List<MediaDownload> downloads);
}
