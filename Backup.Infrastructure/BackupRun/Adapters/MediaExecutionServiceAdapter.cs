using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Media.Abstractions.Services;

namespace Backup.Infrastructure.BackupRun.Adapters;

public sealed class MediaExecutionServiceAdapter(IMediaService mediaService)
    : IMediaExecutionService
{
    private readonly IMediaService _mediaService = mediaService;

    public Task Download(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _mediaService.Download(cancellationToken);
    }
}
