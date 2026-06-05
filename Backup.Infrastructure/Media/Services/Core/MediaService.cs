using Backup.Application.Media;
using Backup.Application.Media.Ports;
using Backup.Infrastructure.Media.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

public class MediaService(
    IMediaOrchestrationService mediaOrchestrationService,
    IMediaOrchestrationCommand mediaOrchestrationCommand,
    ILogger<MediaService> logger
) : IMediaService
{
    private readonly IMediaOrchestrationService _mediaOrchestrationService =
        mediaOrchestrationService;
    private readonly IMediaOrchestrationCommand _mediaOrchestrationCommand =
        mediaOrchestrationCommand;
    private readonly ILogger<MediaService> _logger = logger;

    public async Task Download(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("media service: starting media download orchestration");
        await _mediaOrchestrationService.Run(_mediaOrchestrationCommand, cancellationToken);
        _logger.LogInformation("media service: media download orchestration completed");
    }
}
