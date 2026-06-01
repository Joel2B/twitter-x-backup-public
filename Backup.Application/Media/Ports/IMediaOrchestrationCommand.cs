using Backup.Application.Media.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Media.Ports;

public interface IMediaOrchestrationCommand
{
    Task<IReadOnlyList<MediaInput>> GetMediaInputs(CancellationToken cancellationToken = default);
    Task<MediaProcessingResult> Process(
        IReadOnlyList<MediaInput> posts,
        CancellationToken cancellationToken = default
    );
    Task Prune(List<MediaDownload> downloads, CancellationToken cancellationToken = default);
    Task Filter(List<MediaDownload> downloads, CancellationToken cancellationToken = default);

    IReadOnlyList<string> GetStorageIds();
    bool HasMaintenance(string storageId);
    Task PruneStorage(
        string storageId,
        List<MediaDownload> downloads,
        CancellationToken cancellationToken = default
    );
    Task CheckStorageData(
        string storageId,
        List<MediaDownload> downloads,
        CancellationToken cancellationToken = default
    );
    Task CheckStorageIntegrity(
        string storageId,
        List<MediaDownload> downloads,
        CancellationToken cancellationToken = default
    );
    Task DownloadToStorage(
        string storageId,
        List<MediaDownload> downloads,
        CancellationToken cancellationToken = default
    );
    Task ReplicateFromStorage(
        string storageId,
        List<MediaDownload> downloads,
        CancellationToken cancellationToken = default
    );

    Task RunBackups(List<MediaDownload> downloads, CancellationToken cancellationToken = default);
}
