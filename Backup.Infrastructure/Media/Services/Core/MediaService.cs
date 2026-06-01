using Backup.Application.Media;
using Backup.Infrastructure.Media.Abstractions.Services;

namespace Backup.Infrastructure.Media.Services;

public class MediaService(
    IMediaOrchestrationService mediaOrchestrationService,
    MediaOrchestrationCommandAdapter mediaOrchestrationCommand
) : IMediaService
{
    private readonly IMediaOrchestrationService _mediaOrchestrationService =
        mediaOrchestrationService;
    private readonly MediaOrchestrationCommandAdapter _mediaOrchestrationCommand =
        mediaOrchestrationCommand;

    public Task Download(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _mediaOrchestrationService.Run(_mediaOrchestrationCommand, cancellationToken);
    }
}
