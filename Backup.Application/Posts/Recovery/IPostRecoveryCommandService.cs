using Backup.Application.Posts.Ports;

namespace Backup.Application.Posts;

public interface IPostRecoveryCommandService
{
    Task Execute(IPostRecoveryCommand command, CancellationToken cancellationToken);
}
