using Backup.Infrastructure.Media.Abstractions.Services;

namespace Backup.Infrastructure.Media.Services;

public sealed class MediaOrchestrationCommandDependencies(
    IMediaProcessing mediaProcessing,
    IMediaPrune mediaPrune,
    IEnumerable<IMediaStorage> mediaStorage,
    IEnumerable<IMediaDataMaintenance> mediaMaintenance,
    MediaIntegrity mediaIntegrity,
    IMediaFilter mediaFilter,
    IMediaReplication mediaReplication,
    IEnumerable<IMediaBackupStrategy> mediaBackups,
    IMediaDownloadService mediaDownload,
    IMediaDownloadModelMapper mediaDownloadModelMapper
)
{
    public IMediaProcessing MediaProcessing { get; } = mediaProcessing;
    public IMediaPrune MediaPrune { get; } = mediaPrune;
    public IEnumerable<IMediaStorage> MediaStorage { get; } = mediaStorage;
    public IEnumerable<IMediaDataMaintenance> MediaMaintenance { get; } = mediaMaintenance;
    public MediaIntegrity MediaIntegrity { get; } = mediaIntegrity;
    public IMediaFilter MediaFilter { get; } = mediaFilter;
    public IMediaReplication MediaReplication { get; } = mediaReplication;
    public IEnumerable<IMediaBackupStrategy> MediaBackups { get; } = mediaBackups;
    public IMediaDownloadService MediaDownload { get; } = mediaDownload;
    public IMediaDownloadModelMapper MediaDownloadModelMapper { get; } = mediaDownloadModelMapper;
}
