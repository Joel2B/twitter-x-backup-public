using Backup.Application.Media.Ports;

namespace Backup.Application.Media;

public interface IMediaOrchestrationService
{
    Task Run(IMediaOrchestrationCommand command);
}
